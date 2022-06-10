using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using XUMM.NET.SDK.Clients.Interfaces;
using XUMM.NET.SDK.Enums;
using XUMM.NET.SDK.Extensions;
using XUMM.NET.SDK.Models.Misc;
using XUMM.NET.SDK.Models.Payload;
using XUMM.NET.SDK.Models.Payload.XRPL;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public class XummOrderService : IXummOrderService
    {
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IXummMiscClient _xummMiscClient;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly IXummMailService _xummMailService;
        private readonly INotificationService _notificationService;
        private readonly IXummPayloadClient _xummPayloadClient;
        private readonly IXummService _xummService;
        private readonly XummPaymentSettings _xummPaymentSettings;
        private readonly ILogger _logger;

        public XummOrderService(
            IActionContextAccessor actionContextAccessor,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IUrlHelperFactory urlHelperFactory,
            IXummPayloadClient xummPayloadClient,
            IXummService xummService,
            IXummMiscClient xummMiscClient,
            IStaticCacheManager staticCacheManager,
            IXummMailService xummMailService,
            INotificationService notificationService,
            XummPaymentSettings xummPaymentSettings,
            ILogger logger)
        {
            _actionContextAccessor = actionContextAccessor;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _urlHelperFactory = urlHelperFactory;
            _xummPayloadClient = xummPayloadClient;
            _xummService = xummService;
            _xummMiscClient = xummMiscClient;
            _staticCacheManager = staticCacheManager;
            _xummMailService = xummMailService;
            _notificationService = notificationService;
            _xummPaymentSettings = xummPaymentSettings;
            _logger = logger;
        }

        public async Task<RefundPaymentResult> ProcessRefundPaymentRequestAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var cachkeKey = _staticCacheManager.PrepareKeyForDefaultCache(XummDefaults.RefundCacheKey, refundPaymentRequest.Order.OrderGuid);
            var refundAmounts = await _staticCacheManager.GetAsync(cachkeKey, () => new List<decimal>());

            if (!refundAmounts.Contains(refundPaymentRequest.AmountToRefund))
            {
                var refundUrl = await GetRefundRedirectUrlAsync(refundPaymentRequest);
                var messageQueueId = await _xummMailService.SendRefundMailToStoreOwnerAsync(refundPaymentRequest, refundUrl);

                var errorMessage = string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Refund.MailDetails"), string.Join(", ", messageQueueId));
                return new RefundPaymentResult
                {
                    Errors = new[] { errorMessage }
                };
            }
            else
            {
                return new RefundPaymentResult
                {
                    NewPaymentStatus = refundPaymentRequest.AmountToRefund != refundPaymentRequest.Order.OrderTotal ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded
                };
            }
        }

        public async Task<string> GetPaymentRedirectUrlAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            try
            {
                var paymentTransaction = new XrplPaymentTransaction(_xummPaymentSettings.XrplAddress, _xummPaymentSettings.XrplDestinationTag, XummDefaults.XRPL.Fee);
                if (_xummPaymentSettings.XrplCurrency == XummDefaults.XRPL.XRP)
                {
                    paymentTransaction.SetAmount(postProcessPaymentRequest.Order.OrderTotal);
                }
                else
                {
                    paymentTransaction.SetAmount(_xummPaymentSettings.XrplCurrency, postProcessPaymentRequest.Order.OrderTotal, _xummPaymentSettings.XrplIssuer);
                }

                var returnUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.PaymentProcessorRouteName, new { orderGuid = postProcessPaymentRequest.Order.OrderGuid });
                var attempt = await GetOrderAttemptAsync(postProcessPaymentRequest.Order, true);

                var payload = paymentTransaction.ToXummPostJsonPayload();

                payload.Options = new XummPayloadOptions
                {
                    ReturnUrl = new XummPayloadReturnUrl
                    {
                        Web = returnUrl
                    }
                };

                payload.CustomMeta = new XummPayloadCustomMeta
                {
                    Instruction = await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Payment.Instruction"),
                    Identifier = postProcessPaymentRequest.Order.GetCustomIdentifier(XummPayloadType.Payment, attempt)
                };

                var result = await _xummPayloadClient.CreateAsync(payload, true);
                return result.Next.Always;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
                throw;
            }
        }

        private async Task<(XummPayloadDetails, XummTransaction)> GetOrderPaymentDetailsAsync(Order order)
        {
            var attempt = await GetOrderAttemptAsync(order, false);
            XummTransaction paymentTransaction = null;

            XummPayloadDetails paymentPayload;
            do
            {
                var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Payment, attempt);
                (paymentPayload, var paymentPayloadStatus) = await _xummService.GetPayloadDetailsAsync(customIdentifier);

                if (paymentPayloadStatus == XummPayloadStatus.Signed || paymentPayloadStatus == XummPayloadStatus.ExpiredSigned)
                {
                    (var success, paymentTransaction) = await HasSuccesTransactionAsync(order, XummPayloadType.Payment, paymentPayload.Response.Txid);
                    if (!success)
                    {
                        paymentTransaction = null;
                        paymentPayload = null;
                    }
                }
                else
                {
                    paymentPayload = null;
                }

                attempt--;
            }
            while (attempt >= 1 && (paymentTransaction == null || paymentPayload == null));

            return (paymentPayload, paymentTransaction);
        }

        public async Task<string> GetRefundRedirectUrlAsync(RefundPaymentRequest refundPaymentRequest)
        {
            try
            {
                var (paymentPayload, paymentTransaction) = await GetOrderPaymentDetailsAsync(refundPaymentRequest.Order);
                if (paymentPayload == null || paymentTransaction == null)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {refundPaymentRequest.Order.OrderGuid} has no signed payload.");
                }

                var refundTransaction = new XrplPaymentTransaction(paymentPayload.Response.Account, XummDefaults.XRPL.RefundDestinationTag, XummDefaults.XRPL.Fee)
                {
                    Flags = XrplPaymentFlags.tfPartialPayment
                };

                try
                {
                    var balanceUsed = paymentTransaction.BalanceChanges[paymentPayload.Response.Account].Single();
                    if (string.IsNullOrEmpty(balanceUsed.CounterParty))
                    {
                        refundTransaction.SetAmount(1000000);
                    }
                    else
                    {
                        refundTransaction.SetAmount(balanceUsed.Currency, 1000000, balanceUsed.CounterParty);
                    }
                }
                catch (Exception ex)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {refundPaymentRequest.Order.OrderGuid} has no single currency used for the payment.", ex);
                }

                try
                {
                    var balanceReceived = paymentTransaction.BalanceChanges[paymentPayload.Meta.Destination].Single();
                    var amount = decimal.Parse(balanceReceived.Value, CultureInfo.InvariantCulture);

                    if (refundPaymentRequest.AmountToRefund > amount)
                    {
                        throw new NopException($"{XummDefaults.SystemName} Requested refund is higher than received amount for order with {refundPaymentRequest.Order.OrderGuid}.");
                    }

                    if (string.IsNullOrEmpty(balanceReceived.CounterParty))
                    {
                        refundTransaction.SetSendMaxAmount(refundPaymentRequest.AmountToRefund);
                        //refundTransaction.SetDeliverMinAmount(amount);
                    }
                    else
                    {
                        refundTransaction.SetSendMaxAmount(balanceReceived.Currency, refundPaymentRequest.AmountToRefund, balanceReceived.CounterParty);
                        //refundTransaction.SetDeliverMinAmount(balanceReceived.Currency, amount, balanceReceived.CounterParty);
                    }
                }
                catch (Exception ex)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {refundPaymentRequest.Order.OrderGuid} has not received a single currency as a payment.", ex);
                }

                var count = await GetRefundCountAndAmountAsync(refundPaymentRequest.Order, true);
                var returnUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.RefundProcessorRouteName, new { orderGuid = refundPaymentRequest.Order.OrderGuid, count = count });

                var refundPayload = refundTransaction.ToXummPostJsonPayload();
                refundPayload.Options = new XummPayloadOptions
                {
                    ReturnUrl = new XummPayloadReturnUrl
                    {
                        Web = returnUrl
                    }
                };
                refundPayload.CustomMeta = new XummPayloadCustomMeta
                {
                    Instruction = await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Refund.Instruction"),
                    Identifier = refundPaymentRequest.Order.GetCustomIdentifier(XummPayloadType.Refund, count)
                };

                var result = await _xummPayloadClient.CreateAsync(refundPayload, true);
                return result.Next.Always;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
                throw;
            }
        }

        public async Task<Order> ProcessOrderAsync(Guid orderGuid, bool webhookCall)
        {
            try
            {
                if (await _paymentPluginManager.LoadPluginBySystemNameAsync(XummDefaults.SystemName) is not XummPaymentMethod paymentMethod || !_paymentPluginManager.IsPluginActive(paymentMethod))
                {
                    throw new NopException($"{XummDefaults.SystemName} module cannot be loaded");
                }

                var order = await _orderService.GetOrderByGuidAsync(orderGuid);
                if (order == null)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {orderGuid} can't be found.");
                }

                var attempt = await GetOrderAttemptAsync(order, false);
                var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Payment, attempt);
                var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(customIdentifier);
                if (paymentStatus != XummPayloadStatus.NotFound && !payload.Payload.TxType.Equals(nameof(XrplTransactionType.Payment)))
                {
                    throw new NopException($"{XummDefaults.SystemName} Payload ({customIdentifier}) of order with {orderGuid} has {payload.Payload.TxType} as transaction type.");
                }

                if (webhookCall)
                {
                    await InsertOrderNoteAsync(order, XummPayloadType.Payment, paymentStatus, attempt);
                }

                switch (paymentStatus)
                {
                    case XummPayloadStatus.Signed:
                    case XummPayloadStatus.ExpiredSigned:
                        await MarkOrderAsPaidAsync(order, payload.Response.Txid);
                        break;
                    case XummPayloadStatus.Rejected:
                        await CancelOrderAsync(order);
                        break;
                }

                return order;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
                throw;
            }
        }

        public async Task<Order> ProcessRefundAsync(Guid orderGuid, bool webhookCall, int? count = null)
        {
            try
            {
                if (await _paymentPluginManager.LoadPluginBySystemNameAsync(XummDefaults.SystemName) is not XummPaymentMethod paymentMethod || !_paymentPluginManager.IsPluginActive(paymentMethod))
                {
                    throw new NopException($"{XummDefaults.SystemName} module cannot be loaded");
                }

                var order = await _orderService.GetOrderByGuidAsync(orderGuid);
                if (order == null)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {orderGuid} can't be found.");
                }

                if (!count.HasValue)
                {
                    count = await GetRefundCountAndAmountAsync(order, false);
                }

                var processed = await _genericAttributeService.GetAttributeAsync<List<int>>(order, XummDefaults.OrderRefundProcessedCountsAttributeName) ?? new List<int>();
                if (processed.Contains(count.Value))
                {
                    return order;
                }

                var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Refund, count.Value);
                var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(customIdentifier);
                if (paymentStatus != XummPayloadStatus.NotFound && !payload.Payload.TxType.Equals(nameof(XrplTransactionType.Payment)))
                {
                    throw new NopException($"{XummDefaults.SystemName} Payload ({customIdentifier}) of order with {orderGuid} has {payload.Payload.TxType} as transaction type.");
                }

                if (webhookCall)
                {
                    await InsertOrderNoteAsync(order, XummPayloadType.Refund, paymentStatus, count.Value);
                }

                var setAsProcessed = true;
                if (paymentStatus == XummPayloadStatus.Signed || paymentStatus == XummPayloadStatus.ExpiredSigned)
                {
                    var (success, transaction) = await HasSuccesTransactionAsync(order, XummPayloadType.Refund, payload.Response.Txid);
                    if (success)
                    {
                        var amount = decimal.Zero;

                        if (transaction.BalanceChanges.TryGetValue(payload.Response.Account, out var changes))
                        {
                            foreach (var change in changes)
                            {
                                // TODO: Do we have to match the XRPL CurrencyCode?
                                if (decimal.TryParse(change.Value, out var changedAmount))
                                {
                                    amount += changedAmount;
                                }
                            }
                        }

                        var cachkeKey = _staticCacheManager.PrepareKeyForDefaultCache(XummDefaults.RefundCacheKey, order.OrderGuid);
                        var refundAmounts = await _staticCacheManager.GetAsync(cachkeKey, () => new List<decimal>());

                        if (!refundAmounts.Contains(amount))
                        {
                            refundAmounts.Add(amount);
                        }

                        // Set the refund amount in the cache so we can use the original refund flow
                        // ProcessRefundPaymentRequestAsync will validate the refund amount
                        await _staticCacheManager.SetAsync(cachkeKey, refundAmounts);

                        // We can call partially refund even if it's a full refund because the order total will be checked
                        var errors = await _orderProcessingService.PartiallyRefundAsync(order, amount);
                        if (!webhookCall)
                        {
                            foreach (var error in errors)
                            {
                                _notificationService.ErrorNotification(error);
                            }
                        }
                        setAsProcessed = !errors.Any();
                    }
                }

                if (setAsProcessed)
                {
                    processed = await _genericAttributeService.GetAttributeAsync<List<int>>(order, XummDefaults.OrderRefundProcessedCountsAttributeName) ?? new List<int>();
                    processed.Add(count.Value);
                    await _genericAttributeService.SaveAttributeAsync(order, XummDefaults.OrderRefundProcessedCountsAttributeName, processed);
                }

                return order;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
                throw;
            }
        }

        private async Task MarkOrderAsPaidAsync(Order order, string transactionId)
        {
            if (!_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                return;
            }

            var (success, _) = await HasSuccesTransactionAsync(order, XummPayloadType.Payment, transactionId);
            if (success)
            {
                // Re-validate if the order can still be marked as paid or it has been
                // marked as paid by either the redirected consumer or webhook call
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                }
            }
        }

        private async Task<(bool success, XummTransaction transaction)> HasSuccesTransactionAsync(Order order, XummPayloadType xummPayloadType, string transactionHash)
        {
            var transaction = await _xummMiscClient.GetTransactionAsync(transactionHash);
            if (transaction == null)
            {
                await InsertOrderNoteAsync(order, $"Unable to fetch transaction with hash \"{transactionHash}\".");
                return (false, default);
            }

            if (!(transaction.Transaction?.RootElement.TryGetProperty("meta", out var meta) ?? false) || !meta.TryGetProperty("TransactionResult", out var transactionResult))
            {
                await InsertOrderNoteAsync(order, $"Unable to get 'meta.TransactionResult' property of transaction with hash \"{transactionHash}\".");
                return (false, transaction);
            }

            var result = transactionResult.GetString();
            var success = result.StartsWith(XummDefaults.XRPL.SuccesTransactionResultPrefix);

            var message = string.Format(await _localizationService.GetResourceAsync($"Plugins.Payments.Xumm.{xummPayloadType}.{(success ? "Success" : "Failed")}Transaction"), transactionHash, result);
            await InsertOrderNoteAsync(order, message);

            return (success, transaction);
        }

        private async Task CancelOrderAsync(Order order)
        {
            if (_orderProcessingService.CanCancelOrder(order))
            {
                await _orderProcessingService.CancelOrderAsync(order, true);
            }
        }

        private async Task InsertOrderNoteAsync(Order order, XummPayloadType payloadType, XummPayloadStatus status, int attempt)
        {
            await InsertOrderNoteAsync(order, $"{payloadType}: {status.GetDescription()} (#{attempt})");
        }

        private async Task<int> GetRefundCountAndAmountAsync(Order order, bool increment)
        {
            var count = await _genericAttributeService.GetAttributeAsync<int>(order, XummDefaults.OrderRefundCountAttributeName);

            if (increment)
            {
                await _genericAttributeService.SaveAttributeAsync(order, XummDefaults.OrderRefundCountAttributeName, ++count);
            }

            return count;
        }

        private async Task<int> GetOrderAttemptAsync(Order order, bool increment)
        {
            var attempt = await _genericAttributeService.GetAttributeAsync<int>(order, XummDefaults.OrderPaymentAttemptAttributeName);
            if (increment)
            {
                await _genericAttributeService.SaveAttributeAsync(order, XummDefaults.OrderPaymentAttemptAttributeName, ++attempt);
            }

            return attempt;
        }

        private async Task InsertOrderNoteAsync(Order order, string note, bool displayToCustomer = false)
        {
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = note,
                DisplayToCustomer = displayToCustomer,
                CreatedOnUtc = DateTime.UtcNow
            });
        }
    }
}

using System;
using System.Collections.Generic;
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
using Nop.Plugin.Payments.Xumm.Services.AsyncLock;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Logging;
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
        private readonly AsyncLockService _asyncLockService = new();

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IXummMiscClient _xummMiscClient;
        private readonly IXummMailService _xummMailService;
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
            IStaticCacheManager staticCacheManager,
            IUrlHelperFactory urlHelperFactory,
            IXummPayloadClient xummPayloadClient,
            IXummService xummService,
            IXummMiscClient xummMiscClient,
            IXummMailService xummMailService,
            XummPaymentSettings xummPaymentSettings,
            ILogger logger)
        {
            _actionContextAccessor = actionContextAccessor;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _staticCacheManager = staticCacheManager;
            _urlHelperFactory = urlHelperFactory;
            _xummPayloadClient = xummPayloadClient;
            _xummService = xummService;
            _xummMiscClient = xummMiscClient;
            _xummMailService = xummMailService;
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
                var paymentTransaction = new XrplPaymentTransaction(_xummPaymentSettings.XrplAddress, _xummPaymentSettings.XrplPaymentDestinationTag, XummDefaults.XRPL.Fee);
                if (_xummPaymentSettings.XrplCurrency == XummDefaults.XRPL.XRP)
                {
                    paymentTransaction.SetAmount(postProcessPaymentRequest.Order.OrderTotal);
                }
                else
                {
                    paymentTransaction.SetAmount(_xummPaymentSettings.XrplCurrency, postProcessPaymentRequest.Order.OrderTotal, _xummPaymentSettings.XrplIssuer);
                }

                var returnUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.PaymentProcessorRouteName, new { orderGuid = postProcessPaymentRequest.Order.OrderGuid });
                var attempt = await GetOrderPayloadCountAsync(postProcessPaymentRequest.Order, XummPayloadType.Payment, true);

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
            var attempt = await GetOrderPayloadCountAsync(order, XummPayloadType.Payment, false);
            XummTransaction paymentTransaction = null;

            XummPayloadDetails paymentPayload;
            do
            {
                var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Payment, attempt);
                (paymentPayload, var paymentPayloadStatus) = await _xummService.GetPayloadDetailsAsync(customIdentifier);

                if (paymentPayloadStatus == XummPayloadStatus.Signed || paymentPayloadStatus == XummPayloadStatus.ExpiredSigned)
                {
                    (var success, paymentTransaction) = await HasSuccesTransactionAsync(order, XummPayloadType.Payment, paymentPayload.Response.Txid, false);
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

                var refundTransaction = new XrplPaymentTransaction(paymentPayload.Response.Account, _xummPaymentSettings.XrplRefundDestinationTag, XummDefaults.XRPL.Fee)
                {
                    Flags = XrplPaymentFlags.tfPartialPayment
                };

                try
                {
                    var balanceUsed = paymentTransaction.GetDeductedBalanceChanges(paymentPayload.Response.Account).First();
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
                    throw new NopException($"{XummDefaults.SystemName} Failed to get deducted balance for order with GUID {refundPaymentRequest.Order.OrderGuid}.", ex);
                }

                try
                {
                    var balanceReceived = paymentTransaction.GetReceivedBalanceChanges(paymentPayload.Meta.Destination).First();

                    if (!decimal.TryParse(balanceReceived.Value, out var amount))
                    {
                        throw new NopException($"{XummDefaults.SystemName} Can't parse amount {balanceReceived.Value} of order with GUID {refundPaymentRequest.Order.OrderGuid}.");
                    }

                    if (refundPaymentRequest.AmountToRefund > amount)
                    {
                        throw new NopException($"{XummDefaults.SystemName} Requested refund is higher than received amount for order with GUID {refundPaymentRequest.Order.OrderGuid}.");
                    }

                    if (string.IsNullOrEmpty(balanceReceived.CounterParty))
                    {
                        refundTransaction.SetSendMaxAmount(refundPaymentRequest.AmountToRefund);
                    }
                    else
                    {
                        refundTransaction.SetSendMaxAmount(balanceReceived.Currency, refundPaymentRequest.AmountToRefund, balanceReceived.CounterParty);
                    }
                }
                catch (Exception ex)
                {
                    throw new NopException($"{XummDefaults.SystemName} Failed to get received balance for order with GUID {refundPaymentRequest.Order.OrderGuid}.", ex);
                }

                var count = await GetOrderPayloadCountAsync(refundPaymentRequest.Order, XummPayloadType.Refund, true);
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

        public async Task<Order> ProcessOrderAsync(Guid orderGuid)
        {
            var attempt = 0;
            try
            {
                if (await _paymentPluginManager.LoadPluginBySystemNameAsync(XummDefaults.SystemName) is not XummPaymentMethod paymentMethod || !_paymentPluginManager.IsPluginActive(paymentMethod))
                {
                    throw new NopException($"{XummDefaults.SystemName} module cannot be loaded");
                }

                using (await _asyncLockService.LockAsync(orderGuid))
                {
                    var order = await _orderService.GetOrderByGuidAsync(orderGuid);
                    if (order == null)
                    {
                        throw new NopException($"{XummDefaults.SystemName} Order with {orderGuid} can't be found.");
                    }

                    attempt = await GetOrderPayloadCountAsync(order, XummPayloadType.Payment, false);

                    if (await HasProcessedOrderPayloadAsync(order, XummPayloadType.Payment, attempt))
                    {
                        return order;
                    }

                    var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Payment, attempt);
                    var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(customIdentifier);
                    if (paymentStatus != XummPayloadStatus.NotFound && !payload.Payload.TxType.Equals(nameof(XrplTransactionType.Payment)))
                    {
                        throw new NopException($"{XummDefaults.SystemName} Payload ({customIdentifier}) of order with {orderGuid} has {payload.Payload.TxType} as transaction type.");
                    }

                    await InsertOrderNoteAsync(order, XummPayloadType.Payment, paymentStatus, payload.Response.ResolvedAt, attempt);

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

                    await SetOrderPayloadAsProcessedAsync(order, XummPayloadType.Payment, attempt);

                    return order;
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: Failed to process order with GUID {orderGuid} (#{attempt})", ex);
                throw;
            }
        }

        public async Task<Order> ProcessRefundAsync(Guid orderGuid, int? count)
        {
            try
            {
                if (await _paymentPluginManager.LoadPluginBySystemNameAsync(XummDefaults.SystemName) is not XummPaymentMethod paymentMethod || !_paymentPluginManager.IsPluginActive(paymentMethod))
                {
                    throw new NopException($"{XummDefaults.SystemName} module cannot be loaded");
                }

                using (await _asyncLockService.LockAsync(orderGuid))
                {
                    var order = await _orderService.GetOrderByGuidAsync(orderGuid);
                    if (order == null)
                    {
                        throw new NopException($"{XummDefaults.SystemName} Order with {orderGuid} can't be found.");
                    }

                    if (!count.HasValue)
                    {
                        count = await GetOrderPayloadCountAsync(order, XummPayloadType.Refund, false);
                    }

                    if (await HasProcessedOrderPayloadAsync(order, XummPayloadType.Refund, count.Value))
                    {
                        return order;
                    }

                    var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Refund, count.Value);
                    var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(customIdentifier);
                    if (paymentStatus != XummPayloadStatus.NotFound && !payload.Payload.TxType.Equals(nameof(XrplTransactionType.Payment)))
                    {
                        throw new NopException($"{XummDefaults.SystemName} Payload ({customIdentifier}) of order with {orderGuid} has {payload.Payload.TxType} as transaction type.");
                    }

                    await InsertOrderNoteAsync(order, XummPayloadType.Refund, paymentStatus, payload.Response.ResolvedAt, count.Value);

                    var setAsProcessed = true;
                    if (paymentStatus == XummPayloadStatus.Signed || paymentStatus == XummPayloadStatus.ExpiredSigned)
                    {
                        var (success, transaction) = await HasSuccesTransactionAsync(order, XummPayloadType.Refund, payload.Response.Txid, true);
                        if (success)
                        {
                            var amount = decimal.Zero;

                            var changes = transaction.GetDeductedBalanceChanges(payload.Response.Account);
                            foreach (var change in changes)
                            {
                                var changedAmount = change.Value.XrplStringNumberToDecimal();
                                amount += Math.Abs(changedAmount);
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

                            IList<string> errors;
                            if (amount < order.OrderTotal)
                            {
                                errors = await _orderProcessingService.PartiallyRefundAsync(order, amount);
                            }
                            else
                            {
                                errors = await _orderProcessingService.RefundAsync(order);
                            }

                            setAsProcessed = !errors.Any();
                        }
                    }

                    if (setAsProcessed)
                    {
                        await SetOrderPayloadAsProcessedAsync(order, XummPayloadType.Refund, count.Value);
                    }

                    return order;
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: Failed to refund order with GUID {orderGuid} (#{count})", ex);
                throw;
            }
        }

        private async Task MarkOrderAsPaidAsync(Order order, string transactionId)
        {
            if (!_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                return;
            }

            var (success, _) = await HasSuccesTransactionAsync(order, XummPayloadType.Payment, transactionId, true);
            if (success)
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
            }
        }

        private async Task<(bool success, XummTransaction transaction)> HasSuccesTransactionAsync(Order order, XummPayloadType xummPayloadType, string transactionHash, bool insertNote)
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
            
            if (insertNote)
            {
                var message = string.Format(await _localizationService.GetResourceAsync($"Plugins.Payments.Xumm.{xummPayloadType}.{(success ? "Success" : "Failed")}Transaction"), transactionHash, result);
                await InsertOrderNoteAsync(order, message);
            }

            return (success, transaction);
        }

        private async Task CancelOrderAsync(Order order)
        {
            if (_orderProcessingService.CanCancelOrder(order))
            {
                await _orderProcessingService.CancelOrderAsync(order, true);
            }
        }

        private async Task InsertOrderNoteAsync(Order order, XummPayloadType payloadType, XummPayloadStatus status, DateTime? resolvedAt, int attempt)
        {
            await InsertOrderNoteAsync(order, $"{payloadType}: {status.GetDescription()} (#{attempt})", createdOnUtc: resolvedAt);
        }

        private async Task<int> GetOrderPayloadCountAsync(Order order, XummPayloadType xummPayloadType, bool increment)
        {
            var key = string.Format(XummDefaults.OrderPayloadCountAttributeName, xummPayloadType);
            var count = await _genericAttributeService.GetAttributeAsync<int>(order, key);

            if (increment)
            {
                await _genericAttributeService.SaveAttributeAsync(order, key, ++count);
            }

            return count;
        }

        private async Task<bool> HasProcessedOrderPayloadAsync(Order order, XummPayloadType xummPayloadType, int count)
        {
            var key = string.Format(XummDefaults.OrderPayloadCountProcessedAttributeName, xummPayloadType);
            var processed = await _genericAttributeService.GetAttributeAsync<List<int>>(order, key) ?? new List<int>();
            return processed.Contains(count);
        }

        private async Task SetOrderPayloadAsProcessedAsync(Order order, XummPayloadType xummPayloadType, int count)
        {
            var key = string.Format(XummDefaults.OrderPayloadCountProcessedAttributeName, xummPayloadType);
            var processed = await _genericAttributeService.GetAttributeAsync<List<int>>(order, key) ?? new List<int>();

            if (!processed.Contains(count))
            {
                processed.Add(count);
                await _genericAttributeService.SaveAttributeAsync(order, key, processed);
            }
        }

        private async Task InsertOrderNoteAsync(Order order, string note, bool displayToCustomer = false, DateTime? createdOnUtc = null)
        {
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = note,
                DisplayToCustomer = displayToCustomer,
                CreatedOnUtc = createdOnUtc ?? DateTime.UtcNow
            });
        }
    }
}

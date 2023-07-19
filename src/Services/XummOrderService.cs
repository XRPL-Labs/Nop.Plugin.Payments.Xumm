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
using Nop.Plugin.Payments.Xumm.WebSocket;
using Nop.Plugin.Payments.Xumm.WebSocket.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using XUMM.NET.SDK.Enums;
using XUMM.NET.SDK.Extensions;
using XUMM.NET.SDK.Models.Misc;
using XUMM.NET.SDK.Models.Payload;
using XUMM.NET.SDK.Models.Payload.XRPL;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public class XummOrderService : IXummOrderService
    {
        #region Fields

        private readonly AsyncLockService _asyncLockService = new();

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly ISettingService _settingService;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IXummMailService _xummMailService;
        private readonly IXrplWebSocket _xrplWebSocket;
        private readonly IXummService _xummService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public XummOrderService(
            IActionContextAccessor actionContextAccessor,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            ISettingService settingService,
            IStaticCacheManager staticCacheManager,
            IUrlHelperFactory urlHelperFactory,
            IXummService xummService,
            IXummMailService xummMailService,
            IXrplWebSocket xrplWebSocket,
            ILogger logger)
        {
            _actionContextAccessor = actionContextAccessor;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _settingService = settingService;
            _staticCacheManager = staticCacheManager;
            _urlHelperFactory = urlHelperFactory;
            _xummService = xummService;
            _xummMailService = xummMailService;
            _xrplWebSocket = xrplWebSocket;
            _logger = logger;
        }

        #endregion

        #region Methods

        public async Task<string> GetPaymentRedirectUrlAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            try
            {
                var settings = await _settingService.LoadSettingAsync<XummPaymentSettings>(postProcessPaymentRequest.Order.StoreId);

                var paymentTransaction = new XrplPaymentTransaction(settings.XrplAddress, settings.XrplPaymentDestinationTag, XummDefaults.XRPL.Fee);
                if (settings.XrplCurrency == XummDefaults.XRPL.XRP)
                {
                    paymentTransaction.SetAmount(postProcessPaymentRequest.Order.OrderTotal);
                }
                else
                {
                    paymentTransaction.SetAmount(settings.XrplCurrency, postProcessPaymentRequest.Order.OrderTotal, settings.XrplIssuer);
                }

                var returnUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.PaymentProcessorRouteName, new { orderGuid = postProcessPaymentRequest.Order.OrderGuid });
                var attempt = await GetOrderPayloadCountAsync(postProcessPaymentRequest.Order, XummPayloadType.Payment, true);

                var payload = paymentTransaction.ToXummPostJsonPayload();

                payload.Options = new XummPayloadOptions
                {
                    Pathfinding = settings.XrplPathfinding,
                    PathfindingFallback = settings.XrplPathfindingFallback,
                    ReturnUrl = new XummPayloadReturnUrl
                    {
                        Web = returnUrl,
                        App = returnUrl
                    }
                };

                payload.CustomMeta = new XummPayloadCustomMeta
                {
                    Instruction = string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Payment.Instruction"), postProcessPaymentRequest.Order.Id),
                    Identifier = postProcessPaymentRequest.Order.GetCustomIdentifier(XummPayloadType.Payment, attempt)
                };

                var result = await (await _xummService.GetXummSdk(postProcessPaymentRequest.Order.StoreId)).Payload.CreateAsync(payload, true);
                return result!.Next.Always;
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
                (paymentPayload, var paymentPayloadStatus) = await _xummService.GetPayloadDetailsAsync(order.StoreId, customIdentifier);

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

        public async Task<string> GetRefundRedirectUrlAsync(Guid orderGuid, decimal amountToRefund, bool isPartialRefund)
        {
            try
            {
                var order = await _orderService.GetOrderByGuidAsync(orderGuid);
                if (order == null)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {orderGuid} is not found.");
                }

                var (paymentPayload, paymentTransaction) = await GetOrderPaymentDetailsAsync(order);
                if (paymentPayload == null || paymentTransaction == null)
                {
                    throw new NopException($"{XummDefaults.SystemName} Order with {order.OrderGuid} has no signed payload.");
                }
              
                XummTransactionBalanceChanges balanceUsed;
                XummTransactionBalanceChanges balanceReceived;

                try
                {
                    balanceUsed = paymentTransaction.GetDeductedBalanceChanges(paymentPayload.Response.Account).First();
                }
                catch (Exception ex)
                {
                    throw new NopException($"{XummDefaults.SystemName} Failed to get deducted balance for order with GUID {order.OrderGuid}.", ex);
                }

                try
                {
                    balanceReceived = paymentTransaction.GetReceivedBalanceChanges(paymentPayload.Meta.Destination).First();
                }
                catch (Exception ex)
                {
                    throw new NopException($"{XummDefaults.SystemName} Failed to get received balance for order with GUID {order.OrderGuid}.", ex);
                }

                var settings = await _settingService.LoadSettingAsync<XummPaymentSettings>(order.StoreId);

                var refundTransaction = new XrplPaymentTransaction(paymentPayload.Response.Account, settings.XrplRefundDestinationTag, XummDefaults.XRPL.Fee);

                if (!balanceUsed.IsEqualTo(balanceReceived))
                {
                    refundTransaction.Flags = XrplPaymentFlags.tfNoDirectRipple;

                    var pathFindRequest = new PathFindCreateRequest
                    {
                        SourceAccount = settings.XrplAddress,
                        DestinationAccount = paymentPayload.Response.Account
                    };

                    if (string.IsNullOrEmpty(balanceUsed.CounterParty))
                    {
                        pathFindRequest.SetDestinationToXrp();
                    }
                    else
                    {
                        pathFindRequest.SetDestinationAmountToCounterParty(balanceUsed.Currency, balanceUsed.CounterParty);
                    }

                    if (string.IsNullOrEmpty(balanceReceived.CounterParty))
                    {
                        refundTransaction.SetSendMaxAmount(amountToRefund);
                        pathFindRequest.SetSendMaxAmount(amountToRefund);
                    }
                    else
                    {
                        refundTransaction.SetSendMaxAmount(balanceReceived.Currency, amountToRefund, balanceReceived.CounterParty);
                        pathFindRequest.SetSendMaxAmount(balanceReceived.Currency, amountToRefund, balanceReceived.CounterParty);
                    }

                    var (destinationAmount, paths) = await _xrplWebSocket.GetDestinationAmountAndPathsAsync(pathFindRequest, !string.IsNullOrEmpty(balanceUsed.CounterParty));
                    if (!destinationAmount.HasValue || paths == null)
                    {
                        throw new NopException($"{XummDefaults.SystemName} Unable to find path to refund order with GUID {order.OrderGuid}.");
                    }

                    if (string.IsNullOrEmpty(balanceUsed.CounterParty))
                    {
                        refundTransaction.SetAmount(destinationAmount.Value);
                    }
                    else
                    {
                        refundTransaction.SetAmount(balanceUsed.Currency, destinationAmount.Value, balanceUsed.CounterParty);
                    }

                    refundTransaction.Paths = paths;
                }
                else
                {
                    if (string.IsNullOrEmpty(balanceUsed.CounterParty))
                    {
                        refundTransaction.SetAmount(amountToRefund);
                    }
                    else
                    {
                        refundTransaction.SetAmount(balanceUsed.Currency, amountToRefund, balanceUsed.CounterParty);
                    }
                }

                var count = await GetOrderPayloadCountAsync(order, XummPayloadType.Refund, true);
                var returnUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.RefundProcessorRouteName, new { orderGuid = order.OrderGuid, count });

                var refundPayload = refundTransaction.ToXummPostJsonPayload();
                refundPayload.Options = new XummPayloadOptions
                {
                    ReturnUrl = new XummPayloadReturnUrl
                    {
                        Web = returnUrl,
                        App = returnUrl
                    }
                };

                refundPayload.CustomMeta = new XummPayloadCustomMeta
                {
                    Instruction = string.Format(await _localizationService.GetResourceAsync($"Plugins.Payments.Xumm.{(isPartialRefund ? "PartialRefund" : "Refund")}.Instruction"), order.Id),
                    Identifier = order.GetCustomIdentifier(XummPayloadType.Refund, count)
                };

                var result = await (await _xummService.GetXummSdk(order.StoreId)).Payload.CreateAsync(refundPayload, true);
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
                    var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(order.StoreId, customIdentifier);
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

                    count ??= await GetOrderPayloadCountAsync(order, XummPayloadType.Refund, false);

                    if (await HasProcessedOrderPayloadAsync(order, XummPayloadType.Refund, count.Value))
                    {
                        return order;
                    }

                    var customIdentifier = order.GetCustomIdentifier(XummPayloadType.Refund, count.Value);
                    var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(order.StoreId, customIdentifier);
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

        public async Task<RefundPaymentResult> ProcessRefundPaymentRequestAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var cachkeKey = _staticCacheManager.PrepareKeyForDefaultCache(XummDefaults.RefundCacheKey, refundPaymentRequest.Order.OrderGuid);
            var refundAmounts = await _staticCacheManager.GetAsync(cachkeKey, () => new List<decimal>());

            if (!refundAmounts.Contains(refundPaymentRequest.AmountToRefund))
            {
                var refundUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.StartRefundRouteName, new { orderGuid = refundPaymentRequest.Order.OrderGuid, amountToRefund = refundPaymentRequest.AmountToRefund, isPartialRefund = refundPaymentRequest.IsPartialRefund });
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
            var transaction = await (await _xummService.GetXummSdk(order.StoreId)).Miscellaneous.GetTransactionAsync(transactionHash);
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
            var success = result.StartsWith(XummDefaults.XRPL.SuccessTransactionResultPrefix);

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
            var description = await _localizationService.GetResourceAsync($"Plugins.Payments.Xumm.Payload.{status}");
            await InsertOrderNoteAsync(order, $"{payloadType}: {description} (#{attempt})", createdOnUtc: resolvedAt);
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

    #endregion
}

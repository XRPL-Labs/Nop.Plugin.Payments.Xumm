using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using XUMM.NET.SDK.Clients.Interfaces;
using XUMM.NET.SDK.Enums;
using XUMM.NET.SDK.Models.Payload;
using XUMM.NET.SDK.Models.Payload.XRPL;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public class XummPaymentService : IXummPaymentService
    {
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IXummMiscClient _xummMiscClient;
        private readonly IXummPayloadClient _xummPayloadClient;
        private readonly IXummService _xummService;
        private readonly XummPaymentSettings _xummPaymentSettings;
        private readonly ILogger _logger;

        public XummPaymentService(
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
            _xummPaymentSettings = xummPaymentSettings;
            _logger = logger;
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

                var payload = new XummPostJsonPayload(JsonSerializer.Serialize(paymentTransaction, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }))
                {
                    Options = new XummPayloadOptions
                    {
                        ReturnUrl = new XummPayloadReturnUrl
                        {
                            Web = returnUrl
                        }
                    },
                    CustomMeta = new XummPayloadCustomMeta
                    {
                        Instruction = await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Payment.Instruction"),
                        Identifier = postProcessPaymentRequest.Order.GetCustomIdentifier(XummPayloadType.Payment, attempt)
                    }
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

        public async Task<string> GetRefundRedirectUrlAsync(RefundPaymentRequest refundPaymentRequest)
        {
            try
            {
                return null;
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
                    await InsertOrderNoteAsync(order, paymentStatus, attempt);
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

        private async Task MarkOrderAsPaidAsync(Order order, string transactionId)
        {
            if (!_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                return;
            }

            if (await HasSuccesTransactionAsync(order, transactionId))
            {
                // Re-validate if the order can still be marked as paid or it has been
                // marked as paid by either the redirected consumer or webhook call
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                }
            }
        }

        private async Task<bool> HasSuccesTransactionAsync(Order order, string transactionHash)
        {
            var transaction = await _xummMiscClient.GetTransactionAsync(transactionHash);
            if (transaction == null)
            {
                await InsertOrderNoteAsync(order, $"Unable to fetch transaction with hash \"{transactionHash}\".");
                return false;
            }

            if (!(transaction.Transaction?.RootElement.TryGetProperty("meta", out var meta) ?? false) || !meta.TryGetProperty("TransactionResult", out var transactionResult))
            {
                await InsertOrderNoteAsync(order, $"Unable to get 'meta.TransactionResult' property of transaction with hash \"{transactionHash}\".");
                return false;
            }

            var result = transactionResult.GetString();
            var success = result.StartsWith(XummDefaults.XRPL.SuccesTransactionResultPrefix);

            var message = string.Format(await _localizationService.GetResourceAsync($"Plugins.Payments.Xumm.Payment.{(success ? "Success" : "Failed")}Transaction"), transactionHash, result);
            await InsertOrderNoteAsync(order, message);

            return success;
        }

        private async Task CancelOrderAsync(Order order)
        {
            if (_orderProcessingService.CanCancelOrder(order))
            {
                await _orderProcessingService.CancelOrderAsync(order, true);
            }
        }

        private async Task InsertOrderNoteAsync(Order order, XummPayloadStatus status, int attempt)
        {
            await InsertOrderNoteAsync(order, $"{status.GetDescription()} (#{attempt})");
        }

        /// <summary>
        /// Get the current or incremented paymant attempt for the provided <see cref="Order"/>.
        /// </summary>
        /// <param name="increment">Attempt will be incremented and saved before returned</param>
        private async Task<int> GetOrderAttemptAsync(Order order, bool increment)
        {
            var attempt = await _genericAttributeService.GetAttributeAsync<int>(order, XummDefaults.OrderPaymentAttemptAttributeName);
            if (increment)
            {
                attempt++;
                await _genericAttributeService.SaveAttributeAsync(order, XummDefaults.OrderPaymentAttemptAttributeName, attempt);
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

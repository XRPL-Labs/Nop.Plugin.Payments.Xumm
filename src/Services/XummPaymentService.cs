using System;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using XUMM.NET.SDK.Clients.Interfaces;
using XUMM.NET.SDK.Enums;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public class XummPaymentService : IXummPaymentService
    {
        private readonly IXummMiscClient _xummMiscClient;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IXummService _xummService;
        private readonly ILogger _logger;

        public XummPaymentService(
            IXummMiscClient xummMiscClient,
            IPaymentPluginManager paymentPluginManager,
            ILocalizationService localizationService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IXummService xummService,
            ILogger logger)
        {
            _xummMiscClient = xummMiscClient;
            _paymentPluginManager = paymentPluginManager;
            _localizationService = localizationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _xummService = xummService;
            _logger = logger;
        }

        public async Task<Order> ProcessOrderAsync(string customIdentifier)
        {
            try
            {
                if (await _paymentPluginManager.LoadPluginBySystemNameAsync(Defaults.SystemName) is not XummPaymentMethod paymentMethod || !_paymentPluginManager.IsPluginActive(paymentMethod))
                {
                    throw new NopException($"{Defaults.SystemName} module cannot be loaded");
                }

                if (string.IsNullOrWhiteSpace(customIdentifier) || !Guid.TryParse(customIdentifier, out var orderId))
                {
                    return null;
                }

                var order = await _orderService.GetOrderByGuidAsync(orderId);
                if (order == null)
                {
                    return null;
                }

                var (payload, paymentStatus) = await _xummService.GetPayloadDetailsAsync(orderId.ToString());
                if (paymentStatus != XummPayloadStatus.NotFound && !payload.Payload.TxType.Equals(nameof(XrplTransactionType.Payment)))
                {
                    return null;
                }

                switch (paymentStatus)
                {
                    case XummPayloadStatus.Signed:
                    case XummPayloadStatus.ExpiredSigned:
                        await MarkOrderAsPaidAsync(order, paymentStatus, payload.Response.Txid);
                        break;
                    case XummPayloadStatus.Rejected:
                    case XummPayloadStatus.Cancelled:
                    case XummPayloadStatus.Expired:
                    case XummPayloadStatus.NotFound: // TODO: Should we cancel the order if the paylwhaoad is not found? Could be that the wrong API Credentials were provided
                        await CancelOrderAsync(order, paymentStatus);
                        break;
                    case XummPayloadStatus.NotInteracted:
                        // TODO: What should we do here; Cancel the order?
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(customIdentifier), paymentStatus, $"Not implemented Payload status {paymentStatus} for order with Custom Identifier {customIdentifier}.");
                }

                return order;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"{Defaults.SystemName}: {ex.Message}", ex);
                throw;
            }
        }

        private async Task MarkOrderAsPaidAsync(Order order, XummPayloadStatus paymentStatus, string transactionId)
        {
            if (!_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                return;
            }

            if (await HasSuccesTransactionAsync(order, transactionId))
            {
                await InsertOrderNoteAsync(order, paymentStatus);
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
            }
        }

        private async Task<bool> HasSuccesTransactionAsync(Order order, string transactionHash)
        {
            var transaction = await _xummMiscClient.GetTransactionAsync(transactionHash);
            if (transaction == null)
            {
                // TODO: What should we do in this case, could it be that the transaction has not yet been distributed between clusters?
                await InsertOrderNoteAsync(order, $"Unable to fetch transaction with hash \"{transactionHash}\".");
                return false;
            }

            if (!(transaction.Transaction?.RootElement.TryGetProperty("meta", out var meta) ?? false) || !meta.TryGetProperty("TransactionResult", out var transactionResult))
            {
                await InsertOrderNoteAsync(order, $"Unable to get 'meta.TransactionResult' property of transaction with hash \"{transactionHash}\".");
                return false;
            }

            var result = transactionResult.GetString();
            var success = result.StartsWith(Defaults.XRPL.SuccesTransactionResultPrefix);

            var message = string.Format(await _localizationService.GetResourceAsync($"Plugins.Payments.Xumm.Payment.{(success ? "Success" : "Failed")}Transaction"), transactionHash, result);
            await InsertOrderNoteAsync(order, message);

            return success;
        }

        private async Task CancelOrderAsync(Order order, XummPayloadStatus paymentStatus)
        {
            if (_orderProcessingService.CanCancelOrder(order))
            {
                await InsertOrderNoteAsync(order, paymentStatus);

                // TODO: Do we want to notify the user or make this a setting?
                await _orderProcessingService.CancelOrderAsync(order, false);
            }
        }

        private async Task InsertOrderNoteAsync(Order order, XummPayloadStatus status)
        {
            await InsertOrderNoteAsync(order, status.GetDescription());
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

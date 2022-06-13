using System;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Services.Logging;
using XUMM.NET.SDK.Webhooks;
using XUMM.NET.SDK.Webhooks.Models;

namespace Nop.Plugin.Payments.Xumm.Services;

public class XummWebhookProcessor : IXummWebhookProcessor
{
    private readonly IXummOrderService _xummPaymentService;
    private readonly ILogger _logger;

    public XummWebhookProcessor(
        IXummOrderService xummPaymentService,
        ILogger logger)
    {
        _xummPaymentService = xummPaymentService;
        _logger = logger;
    }

    public async Task ProcessAsync(XummWebhookBody xummWebhookBody)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(xummWebhookBody.CustomMeta.Identifier))
            {
                return;
            }

            var (orderGuid, payloadType, count) = OrderExtensions.ParseCustomIdentifier(xummWebhookBody.CustomMeta.Identifier);
            if (orderGuid != default)
            {
                if (payloadType == XummPayloadType.Payment)
                {
                    await _xummPaymentService.ProcessOrderAsync(orderGuid);
                }
                else if (payloadType == XummPayloadType.Refund)
                {
                    await _xummPaymentService.ProcessRefundAsync(orderGuid, count);
                }
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
        }
    }
}

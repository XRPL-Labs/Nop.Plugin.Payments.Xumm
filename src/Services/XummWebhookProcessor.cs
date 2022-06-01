using System;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Services.Logging;
using XUMM.NET.SDK.Webhooks;
using XUMM.NET.SDK.Webhooks.Models;

namespace Nop.Plugin.Payments.Xumm.Services;

public class XummWebhookProcessor : IXummWebhookProcessor
{
    private readonly IXummPaymentService _xummPaymentService;
    private readonly ILogger _logger;

    public XummWebhookProcessor(
        IXummPaymentService xummPaymentService,
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

            var (orderGuid, _) = OrderExtensions.ParseCustomIdentifier(xummWebhookBody.CustomMeta.Identifier);
            if (orderGuid != default)
            {
                await _xummPaymentService.ProcessOrderAsync(orderGuid, true);
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{Defaults.SystemName}: {ex.Message}", ex);
        }
    }
}

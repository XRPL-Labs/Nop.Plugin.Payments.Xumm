using System;
using System.Threading.Tasks;
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

            await _xummPaymentService.ProcessOrderAsync(xummWebhookBody.CustomMeta.Identifier);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{Defaults.SystemName}: {ex.Message}", ex);
        }
    }
}

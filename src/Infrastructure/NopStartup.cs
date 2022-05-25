using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Plugin.Payments.Xumm.WebSocket;
using XUMM.NET.SDK;
using XUMM.NET.SDK.Extensions;
using XUMM.NET.SDK.Webhooks;

namespace Nop.Plugin.Payments.Xumm.Infrastructure;

/// <summary>
/// Represents object for the configuring services on application startup
/// </summary>
public class NopStartup : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="configuration">Configuration of the application</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IXrplWebSocket, XrplWebSocket>();
        services.AddScoped<IXummService, XummService>();
        services.AddScoped<IXummPaymentService, XummPaymentService>();
        services.AddScoped<IPaymentInfoFactory, PaymentInfoFactory>();

        var paymentSettings = services.BuildServiceProvider().GetRequiredService<XummPaymentSettings>();

        services.AddXummNet(o =>
        {
            if (paymentSettings.ApiKey.IsValidUuid())
            {
                o.ApiKey = paymentSettings.ApiKey;
            }

            if (paymentSettings.ApiSecret.IsValidUuid())
            {
                o.ApiSecret = paymentSettings.ApiSecret;
            }
        });

        services.AddXummWebhooks<XummWebhookProcessor>();
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 3000;
}

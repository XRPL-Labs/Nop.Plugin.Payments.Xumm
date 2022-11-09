using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Plugin.Payments.Xumm.WebSocket;
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
        services.AddScoped<IXummOrderService, XummOrderService>();
        services.AddScoped<IXummMailService, XummMailService>();

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

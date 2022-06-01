using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;
using XUMM.NET.SDK.Webhooks;

namespace Nop.Plugin.Payments.Xumm.Infrastructure;

/// <summary>
/// Represents plugin route provider
/// </summary>
public class RouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(Defaults.ConfigurationRouteName, "Plugins/Xumm/Configure",
            new
            {
                controller = "XummConfiguration",
                action = "Configure",
                area = AreaNames.Admin
            });

        endpointRouteBuilder.MapControllerRoute(Defaults.ProcessPayloadRouteName, "Plugins/Xumm/ProcessPayload/{customIdentifier}",
            new
            {
                controller = "XummConfiguration",
                action = "ProcessPayload",
                area = AreaNames.Admin
            });

        endpointRouteBuilder.MapControllerRoute(Defaults.PaymentProcessorRouteName, "Plugins/Xumm/ProcessPayment/{orderGuid}",
            new
            {
                controller = "Xumm",
                action = "ProcessPayment"
            });

        endpointRouteBuilder.MapXummControllerRoute(Defaults.WebHooks.RouteName, "Plugins/Xumm/Webhook");
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}

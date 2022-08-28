using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Services.Logging;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Xumm.Controllers;

[AutoValidateAntiforgeryToken]
public class XummController : BasePaymentController
{
    private readonly IPermissionService _permissionService;
    private readonly IXummOrderService _xummOrderService;
    private readonly ILogger _logger;

    public XummController(
        IPermissionService permissionService,
        IXummOrderService xummOrderService,
        ILogger logger)
    {
        _permissionService = permissionService;
        _xummOrderService = xummOrderService;
        _logger = logger;
    }

    public async Task<IActionResult> ProcessPaymentAsync(Guid orderGuid)
    {
        try
        {
            var order = await _xummOrderService.ProcessOrderAsync(orderGuid);

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }

            return RedirectToRoute("OrderDetails", new { orderId = order.Id });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }
    }

    public async Task<IActionResult> ProcessRefundAsync(Guid orderGuid, int count)
    {
        try
        {
            var order = await _xummOrderService.ProcessRefundAsync(orderGuid, count);
            return RedirectToAction("Edit", "Order", new { id = order.Id, area = AreaNames.Admin });
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
            return RedirectToAction("Index", "Log", new { area = AreaNames.Admin });
        }
    }

    public async Task<IActionResult> StartRefundAsync(Guid orderGuid, decimal amountToRefund, bool isPartialRefund)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        {
            return AccessDeniedView();
        }

        try
        {
            var redirectUrl = await _xummOrderService.GetRefundRedirectUrlAsync(orderGuid, amountToRefund, isPartialRefund);
            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
            return RedirectToAction("Index", "Log", new { area = AreaNames.Admin });
        }
    }
}

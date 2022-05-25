using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Xumm.Controllers;

[AutoValidateAntiforgeryToken]
public class XummController : BasePaymentController
{
    private readonly IXummPaymentService _xummPaymentService;

    public XummController(IXummPaymentService xummPaymentService)
    {
        _xummPaymentService = xummPaymentService;
    }

    public async Task<IActionResult> ProcessPaymentAsync(string customIdentifier)
    {
        var order = await _xummPaymentService.ProcessOrderAsync(customIdentifier);

        if (order == null)
        {
            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }
        else if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        return RedirectToRoute("OrderDetails", new { orderId = order.Id });
    }
}

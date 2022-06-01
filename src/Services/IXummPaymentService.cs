using System;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public interface IXummPaymentService
    {
        Task<string> GetPaymentRedirectUrlAsync(PostProcessPaymentRequest postProcessPaymentRequest);

        Task<Order> ProcessOrderAsync(Guid orderGuid, bool webhookCall);
    }
}

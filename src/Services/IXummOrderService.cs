using System;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public interface IXummOrderService
    {
        Task<RefundPaymentResult> ProcessRefundPaymentRequestAsync(RefundPaymentRequest refundPaymentRequest);
        Task<string> GetPaymentRedirectUrlAsync(PostProcessPaymentRequest postProcessPaymentRequest);
        Task<string> GetRefundRedirectUrlAsync(RefundPaymentRequest refundPaymentRequest);
        Task<Order> ProcessOrderAsync(Guid orderGuid);
        Task<Order> ProcessRefundAsync(Guid orderGuid, int? count);
    }
}

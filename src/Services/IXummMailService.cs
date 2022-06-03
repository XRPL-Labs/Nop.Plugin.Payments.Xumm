using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public interface IXummMailService
    {
        Task<IList<int>> SendRefundMailToStoreOwnerAsync(RefundPaymentRequest refundPaymentRequest, int languageId);
    }
}

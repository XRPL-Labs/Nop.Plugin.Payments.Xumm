using System.Threading.Tasks;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.Xumm.Services
{
    public interface IXummPaymentService
    {
        /// <summary>
        /// Process the order based on the identifier.
        /// </summary>
        /// <returns>The <see cref="Task" /> containing the <see cref="Order" /></returns>
        Task<Order> ProcessOrderAsync(string customIdentifier);
    }
}

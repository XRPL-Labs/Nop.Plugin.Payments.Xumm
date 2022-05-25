using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.Models;

namespace Nop.Plugin.Payments.Xumm.Services;

/// <summary>
/// Provides an abstraction for factory to create the <see cref="PaymentInfoModel" />
/// </summary>
public interface IPaymentInfoFactory
{
    /// <summary>
    /// Creates the payment info model
    /// </summary>
    /// <returns>The <see cref="Task" /> containing the <see cref="PaymentInfoModel" /></returns>
    Task<PaymentInfoModel> CreatePaymentInfoAsync();
}

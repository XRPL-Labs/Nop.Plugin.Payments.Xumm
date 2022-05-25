using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.Xumm.Models;
using Nop.Services.Logging;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Xumm.Services;

/// <summary>
/// Provides an default implementation for factory to create the payment info model
/// </summary>
public class PaymentInfoFactory : IPaymentInfoFactory
{
    #region Properties

    private readonly IXummService _xummService;
    private readonly IPaymentService _paymentService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWorkContext _workContext;
    private readonly ILogger _logger;

    #endregion

    #region Ctor

    public PaymentInfoFactory(
        IXummService xummService,
        IPaymentService paymentService,
        IHttpContextAccessor httpContextAccessor,
        IWorkContext workContext,
        ILogger logger)
    {
        _xummService = xummService;
        _paymentService = paymentService;
        _workContext = workContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates the payment info model
    /// </summary>
    /// <returns>The <see cref="Task" /> containing the payment info model</returns>
    public virtual async Task<PaymentInfoModel> CreatePaymentInfoAsync()
    {
        Customer customer = null;
        try
        {
            if (await _xummService.HidePaymentMethodAsync())
            {
                return null;
            }

            customer = await _workContext.GetCurrentCustomerAsync();
            if (customer == null)
            {
                return null;
            }

            var response = new PaymentInfoModel
            {
                AppStoreUrl = Defaults.Xumm.AppStoreUrl,
                GooglePlayUrl = Defaults.Xumm.GooglePlayUrl
            };

            var processPaymentRequest = new ProcessPaymentRequest();
            _paymentService.GenerateOrderGuid(processPaymentRequest);
            _httpContextAccessor.HttpContext.Session.Set(Defaults.PaymentRequestSessionKey, processPaymentRequest);

            return response;
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{Defaults.SystemName}: {ex.Message}", ex, customer);
        }

        return null;
    }

    #endregion
}

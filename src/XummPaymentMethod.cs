using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.Xumm;

/// <summary>
/// Represents a payment method implementation
/// </summary>
public class XummPaymentMethod : BasePlugin, IPaymentMethod
{
    #region Fields

    private readonly IXummPaymentService _xummPaymentService;
    private readonly IXummService _xummService;
    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly XummPaymentSettings _xummPaymentSettings;
    private readonly WidgetSettings _widgetSettings;

    #endregion

    #region Ctor

    public XummPaymentMethod(
        IXummPaymentService xummPaymentService,
        IXummService xummService,
        IActionContextAccessor actionContextAccessor,
        IOrderTotalCalculationService orderTotalCalculationService,
        ILocalizationService localizationService,
        ISettingService settingService,
        IUrlHelperFactory urlHelperFactory,
        IHttpContextAccessor httpContextAccessor,
        IOrderProcessingService orderProcessingService,
        XummPaymentSettings xummPaymentSettings,
        WidgetSettings widgetSettings)
    {
        _xummPaymentService = xummPaymentService;
        _xummService = xummService;
        _actionContextAccessor = actionContextAccessor;
        _orderTotalCalculationService = orderTotalCalculationService;
        _localizationService = localizationService;
        _settingService = settingService;
        _urlHelperFactory = urlHelperFactory;
        _httpContextAccessor = httpContextAccessor;
        _orderProcessingService = orderProcessingService;
        _xummPaymentSettings = xummPaymentSettings;
        _widgetSettings = widgetSettings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Process a payment
    /// </summary>
    /// <param name="processPaymentRequest">Payment info required for an order processing</param>
    /// A task that represents the asynchronous operation
    /// The task result contains the process payment result
    /// </returns>
    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult());
    }

    /// <summary>
    /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
    /// </summary>
    /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        var redirectUrl = await _xummPaymentService.GetPaymentRedirectUrlAsync(postProcessPaymentRequest);
        _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
    }

    /// <summary>
    /// Returns a value indicating whether payment method should be hidden during checkout
    /// </summary>
    /// <param name="cart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the rue - hide; false - display.
    /// </returns>
    public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        return await _xummService.HidePaymentMethodAsync();
    }

    /// <summary>
    /// Gets additional handling fee
    /// </summary>
    /// <param name="cart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the additional handling fee
    /// </returns>
    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        if (cart == null)
        {
            throw new ArgumentNullException(nameof(cart));
        }

        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
            _xummPaymentSettings.AdditionalFee, _xummPaymentSettings.AdditionalFeePercentage);
    }

    /// <summary>
    /// Captures payment
    /// </summary>
    /// <param name="capturePaymentRequest">Capture payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the capture payment result
    /// </returns>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult
        {
            Errors = new[]
            {
                "Capture method not supported"
            }
        });
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    /// <param name="refundPaymentRequest">Request</param>
    /// <returns>The asynchronous task whose result contains the Refund payment result</returns>
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        if (refundPaymentRequest == null)
        {
            throw new ArgumentNullException(nameof(refundPaymentRequest));
        }

        return Task.FromResult(new RefundPaymentResult
        {
            Errors = new[]
            {
                "Refund method not (yet) supported"
            }
        });
    }

    /// <summary>
    /// Voids a payment
    /// </summary>
    /// <param name="voidPaymentRequest">Request</param>
    /// <returns>The asynchronous task whose result contains the Void payment result</returns>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult
        {
            Errors = new[]
            {
                "Void method not supported"
            }
        });
    }

    /// <summary>
    /// Process recurring payment
    /// </summary>
    /// <param name="processPaymentRequest">Payment info required for an order processing</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the process payment result
    /// </returns>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult
        {
            Errors = new[]
            {
                "Recurring payment not supported"
            }
        });
    }

    /// <summary>
    /// Cancels a recurring payment
    /// </summary>
    /// <param name="cancelPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(
        CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult
        {
            Errors = new[]
            {
                "Recurring payment not supported"
            }
        });
    }

    /// <summary>
    /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for
    /// redirection payment methods)
    /// </summary>
    /// <param name="order">Order</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        return Task.FromResult(_orderProcessingService.CanMarkOrderAsPaid(order));
    }

    /// <summary>
    /// Validate payment form
    /// </summary>
    /// <param name="form">The parsed form values</param>
    /// <returns>The asynchronous task whose result contains the List of validating errors</returns>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        return Task.FromResult<IList<string>>(new List<string>());
    }

    /// <summary>
    /// Get payment information
    /// </summary>
    /// <param name="form">The parsed form values</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the payment info holder
    /// </returns>
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        return Task.FromResult(new ProcessPaymentRequest());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(Defaults.ConfigurationRouteName);
    }

    /// <summary>
    /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component name</returns>
    public string GetPublicViewComponentName()
    {
        return null;
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        if (!_widgetSettings.ActiveWidgetSystemNames.Contains(Defaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add(Defaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.Xumm.Section.ApiSettings"] = "API Settings",
            ["Plugins.Payments.Xumm.Section.XrplSettings"] = "XRPL Settings",
            ["Plugins.Payments.Xumm.Section.AdditionalSettings"] = "Additional Settings",
            ["Plugins.Payments.Xumm.Section.ApiSettings.Instructions"] = @"
                    <div style=""margin: 0 0 20px;"">
                        For plugin configuration, follow these steps:<br />
                        <br />
                        1. You will need a Xumm Developer account. If you don't already have one, you can sign up here: <a href=""https://apps.xumm.dev/"" target=""_blank"">https://apps.xumm.dev/</a><br />
                        2. Sign in to 'Xumm Developer Dashboard'. Go to 'Settings' tab, copy 'API Key', 'API Secret' and paste it into the same fields below.<br />
                        3. Update the webhook with URL <em>{0}</em> on the 'Application details' section of 'Settings'.<br />
                        4. The application will restart to apply the API Credentials and the other necessary settings will be visible on valid credentials.<br />
                    </div>",
            ["Plugins.Payments.Xumm.Section.XrplSettings.Instructions"] = @"
                    <div style=""margin: 0 0 20px;"">
                        For payment configuration, follow these steps:<br />
                        <br />
                        1. Set your XRPL Address manually or by clicking the 'Sign in with Xumm' button.<br />
                        2. Select the desired XRPL Currency to receive in the list that is populated with XRP, Curated Assets, Trust Lines of the XRPL Address.<br />
                        3. You will be redirected to the 'TrustSet' flow if you selected a Curated Asset wihout a Trust Line being set on the XRPL Address.<br />
                    </div>",
            ["Plugins.Payments.Xumm.Fields.ApiKey"] = "API Key",
            ["Plugins.Payments.Xumm.Fields.ApiKey.Hint"] = "Enter the API Key for the live environment.",
            ["Plugins.Payments.Xumm.Fields.ApiKey.Required"] = "API Key is required",
            ["Plugins.Payments.Xumm.Fields.ApiSecret"] = "API Secret",
            ["Plugins.Payments.Xumm.Fields.ApiSecret.Hint"] = "Enter the API Secret for the live environment.",
            ["Plugins.Payments.Xumm.Fields.ApiSecret.Required"] = "API Secret is required",
            ["Plugins.Payments.Xumm.Fields.WebhookUrl"] = "Webhook URL",
            ["Plugins.Payments.Xumm.Fields.WebhookUrl.Hint"] = "Update the webhook with URL at the 'Application details' section of 'Settings' in the Xumm Developer Dashboard.",
            ["Plugins.Payments.Xumm.Fields.WebhookUrl.NotConfigured"] = "Webhook URL has not been configured in the Xumm Developer Dashboard.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress"] = "XRPL Address",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Hint"] = "Accounts in the XRP Ledger are identified by an address in the XRP Ledger's base58 format, such as rf1BiGeXwwQoi8Z2ueFYTEXSwuJYfV2Jpn.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.SignInWithXumm"] = "Sign In with Xumm",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Invalid"] = "XRPL Address {0} is an invalid XRPL Address format.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.TrustLinesFailed"] = "Unable to retrieve trust lines of XRPL Address {0}.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Wrong"] = "XRPL Address {0} was used to sign instead of {1}.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Set"] = "XRPL Address {0} has been set.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.FallBackNote"] = "Changing the XRPL Address could set the XRPL Currency to {0}; either if there was no currency selected or no trust line set.",
            ["Plugins.Payments.Xumm.Fields.XrplDestinationTag"] = "XRPL Destination Tag",
            ["Plugins.Payments.Xumm.Fields.XrplDestinationTag.Hint"] = "Arbitrary tag that identifies the reason for the payment to the destination, or a hosted recipient to pay.",
            ["Plugins.Payments.Xumm.Fields.XrplDestinationTag.Invalid"] = "Destination Tag {0} is invalid.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency"] = "XRPL Currency",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.Hint"] = "Here you can select the currency you want to be paid in.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.Required"] = "Currency is required",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.MissingTrustLine"] = "Missing trust line for selected currency code {0}.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.SetTrustLine"] = "Set Trust Line",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.FallBackSet"] = "XRPL Currency is set to {0}.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.TrustLineHeader"] = "Others",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.TrustLineSet"] = "Trust line for currency {0} of issuer {1} has been set.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.MissingPrimaryStoreCurrency"] = "Store currency {0} has to exist and set as Primary Store Currency at Configuration > Currencies.",
            ["Plugins.Payments.Xumm.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.Xumm.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.Xumm.Fields.AdditionalFee.ShouldBeGreaterThanOrEqualZero"] = "The additional fee should be greater than or equal 0",
            ["Plugins.Payments.Xumm.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.Xumm.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.Xumm.Payment.SuccessTransaction"] = "Order payment with transaction hash {0} has been processed.",
            ["Plugins.Payments.Xumm.Payment.FailedTransaction"] = "Order payment with transaction hash {0} has failed with code {1}.",
            ["Plugins.Payments.Xumm.Payment.Instruction"] = "Pay with Xumm",
            ["Plugins.Payments.Xumm.PaymentMethodDescription"] = "Pay with Xumm",
            ["Plugins.Payments.Xumm.PaymentInfo.IsNotConfigured"] = "Plugin is not configured correctly.",
            ["Plugins.Payments.Xumm.Payment.Successful"] = "We have received your payment. Thanks!"
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(Defaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(Defaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await _settingService.DeleteSettingAsync<XummPaymentSettings>();

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Xumm");

        await base.UninstallAsync();
    }

    /// <summary>
    /// Gets a payment method description that will be displayed on checkout pages in the public store
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.PaymentMethodDescription");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether capture is supported
    /// </summary>
    public bool SupportCapture => false;

    /// <summary>
    /// Gets a value indicating whether partial refund is supported
    /// </summary>
    public bool SupportPartiallyRefund => false;

    /// <summary>
    /// Gets a value indicating whether refund is supported
    /// </summary>
    public bool SupportRefund => false;

    /// <summary>
    /// Gets a value indicating whether void is supported
    /// </summary>
    public bool SupportVoid => false;

    /// <summary>
    /// Gets a recurring payment type of payment method
    /// </summary>
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    /// <summary>
    /// Gets a payment method type
    /// </summary>
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    /// <summary>
    /// Gets a value indicating whether we should display a payment information page for this plugin
    /// </summary>
    public bool SkipPaymentInfo => true;

    #endregion
}

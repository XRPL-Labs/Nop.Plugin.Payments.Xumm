using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
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

    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly IEmailAccountService _emailAccountService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalizationService _localizationService;
    private readonly IMessageTemplateService _messageTemplateService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IXummOrderService _xummPaymentService;
    private readonly IXummService _xummService;
    private readonly EmailAccountSettings _emailAccountSettings;
    private readonly WidgetSettings _widgetSettings;
    private readonly XummPaymentSettings _xummPaymentSettings;

    #endregion

    #region Ctor

    public XummPaymentMethod(
        IActionContextAccessor actionContextAccessor,
        IEmailAccountService emailAccountService,
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        IMessageTemplateService messageTemplateService,
        IOrderProcessingService orderProcessingService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IStoreContext storeContext,
        IUrlHelperFactory urlHelperFactory,
        IXummService xummService,
        IXummOrderService xummPaymentService,
        EmailAccountSettings emailAccountSettings,
        WidgetSettings widgetSettings,
        XummPaymentSettings xummPaymentSettings)
    {
        _actionContextAccessor = actionContextAccessor;
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
        _messageTemplateService = messageTemplateService;
        _orderProcessingService = orderProcessingService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _storeContext = storeContext;
        _urlHelperFactory = urlHelperFactory;
        _widgetSettings = widgetSettings;
        _xummPaymentService = xummPaymentService;
        _xummPaymentSettings = xummPaymentSettings;
        _xummService = xummService;
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
        var store = await _storeContext.GetCurrentStoreAsync();
        return await _xummService.HidePaymentMethodAsync(store.Id);
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
    public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        if (refundPaymentRequest == null)
        {
            throw new ArgumentNullException(nameof(refundPaymentRequest));
        }

        return await _xummPaymentService.ProcessRefundPaymentRequestAsync(refundPaymentRequest);
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

    public Type GetPublicViewComponent()
    {
        return null;
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(XummDefaults.ConfigurationRouteName);
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
        var defaultSettings = new XummPaymentSettings
        {
            XrplCurrency = XummDefaults.XRPL.XRP,
            XrplIssuer = XummDefaults.XRPL.XRP
        };

        await _settingService.SaveSettingAsync(defaultSettings);

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
                    </div>",
            ["Plugins.Payments.Xumm.Section.XrplSettings.Instructions"] = @"
                    <div style=""margin: 0 0 20px;"">
                        For payment configuration, follow these steps:<br />
                        <br />
                        1. Set your XRPL Address manually or by clicking the 'Sign in with Xumm' button.<br />
                        2. Select the desired XRPL Currency to receive in the list that is populated with XRP, Curated Assets, Trust Lines of the XRPL Address.<br />
                        3. You will be redirected to the 'TrustSet' flow if you selected a Curated Asset wihout a Trust Line being set on the XRPL Address.<br />
                    </div>",
            ["Plugins.Payments.Xumm.Button.ShowHideSecrets"] = "Show/Hide Secrets",
            ["Plugins.Payments.Xumm.Fields.ApiKey"] = "API Key",
            ["Plugins.Payments.Xumm.Fields.ApiKey.Hint"] = "Enter the API Key for the live environment.",
            ["Plugins.Payments.Xumm.Fields.ApiKey.Required"] = "API Key is required",
            ["Plugins.Payments.Xumm.Fields.ApiKey.Invalid"] = "API Key format is invalid",
            ["Plugins.Payments.Xumm.Fields.ApiSecret"] = "API Secret",
            ["Plugins.Payments.Xumm.Fields.ApiSecret.Hint"] = "Enter the API Secret for the live environment.",
            ["Plugins.Payments.Xumm.Fields.ApiSecret.Required"] = "API Secret is required",
            ["Plugins.Payments.Xumm.Fields.ApiSecret.Invalid"] = "API Secret format is invalid",
            ["Plugins.Payments.Xumm.Fields.WebhookUrl"] = "Webhook URL",
            ["Plugins.Payments.Xumm.Fields.WebhookUrl.Hint"] = "Update the webhook with URL at the 'Application details' section of 'Settings' in the Xumm Developer Dashboard.",
            ["Plugins.Payments.Xumm.Fields.WebhookUrl.NotConfigured"] = "Webhook URL has not been configured in the Xumm Developer Dashboard.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress"] = "R-Address",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Hint"] = "Accounts in the XRP Ledger are identified by an address in the XRP Ledger's base58 format, such as rf1BiGeXwwQoi8Z2ueFYTEXSwuJYfV2Jpn.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.SignInWithXumm"] = "Sign In with Xumm",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.SignInWithXummInstruction"] = "Sign In with Xumm for nopCommerce plugin.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Invalid"] = "XRPL Address is an invalid format.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.TrustLinesFailed"] = "Unable to retrieve trust lines of XRPL Address {0}.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Wrong"] = "XRPL Address {0} was used to sign instead of {1}.",
            ["Plugins.Payments.Xumm.Fields.XrplAddress.Set"] = "XRPL Address {0} has been set.",
            ["Plugins.Payments.Xumm.Fields.XrplPaymentDestinationTag"] = "Payment Destination Tag",
            ["Plugins.Payments.Xumm.Fields.XrplPaymentDestinationTag.Hint"] = "Arbitrary tag that identifies the reason for the payment to the destination, or a hosted recipient to pay.",
            ["Plugins.Payments.Xumm.Fields.XrplRefundDestinationTag"] = "Refund Destination Tag",
            ["Plugins.Payments.Xumm.Fields.XrplRefundDestinationTag.Hint"] = "Arbitrary tag that identifies the reason for the refund to the destination, or a hosted recipient to pay.",
            ["Plugins.Payments.Xumm.Fields.XrplDestinationTag.Invalid"] = "A destination tag is a value between 0 to 4,294,967,295.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency"] = "Currency",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.Hint"] = "Here you can select the XRPL Currency you want to be paid in.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.Required"] = "Currency is required",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.MissingTrustLine"] = "Missing trust line for selected currency code {0}.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.SetTrustLine"] = "Set Trust Line",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.SetTrustLineInstruction"] = "Set TrustLine for nopCommerce payments in {0} of issuer {1}.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.FallBackSet"] = "XRPL Currency is set to {0}.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.TrustLineHeader"] = "Others",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.TrustLineSet"] = "Trust line for currency {0} of issuer {1} has been set.",
            ["Plugins.Payments.Xumm.Fields.XrplCurrency.MissingPrimaryStoreCurrency"] = "Store currency {0} has to exist and set as Primary Store Currency at Configuration > Currencies.",
            ["Plugins.Payments.Xumm.Fields.XrplPathfinding"] = "Pathfinding",
            ["Plugins.Payments.Xumm.Fields.XrplPathfinding.Hint"] = "XRPL Pathfinding simplifies accepting payments in various currencies by finding the best exchange rates and routes, ensuring quick and efficient transactions for your business.",
            ["Plugins.Payments.Xumm.Fields.XrplPathfindingFallback"] = "Pathfinding fallback",
            ["Plugins.Payments.Xumm.Fields.XrplPathfindingFallback.Hint"] = "Pathfinding fallback enables older Xumm clients (version < 2.4.0) to process payments using a native 1:1 asset exchange instead of the modern pathfinding UX. This may result in receiving less than the requested amount due to less efficient currency conversions and potential price fluctuations during the transaction process.",
            ["Plugins.Payments.Xumm.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.Xumm.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.Xumm.Fields.AdditionalFee.ShouldBeGreaterThanOrEqualZero"] = "The additional fee should be greater than or equal 0",
            ["Plugins.Payments.Xumm.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.Xumm.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.Xumm.Payload.NotFound"] = "Sign request cannot be found or belongs to another application (API credentials).",
            ["Plugins.Payments.Xumm.Payload.Signed"] = "Sign request has been resolved by the user by signing the sign request.",
            ["Plugins.Payments.Xumm.Payload.Rejected"] = "Sign request has been resolved by the user by rejecting the sign request.",
            ["Plugins.Payments.Xumm.Payload.Cancelled"] = "Sign request has been cancelled, user didn't interact.",
            ["Plugins.Payments.Xumm.Payload.Expired"] = "Sign request expired, user didn't interact.",
            ["Plugins.Payments.Xumm.Payload.ExpiredSigned"] = "Sign request expired, but user opened before expiration and resolved by signing after expiration.",
            ["Plugins.Payments.Xumm.Payload.NotInteracted"] = "User did not interact with the QR code.",
            ["Plugins.Payments.Xumm.Payment.SuccessTransaction"] = "Order payment with transaction hash {0} has been processed.",
            ["Plugins.Payments.Xumm.Payment.FailedTransaction"] = "Order payment with transaction hash {0} has failed with code {1}.",
            ["Plugins.Payments.Xumm.Refund.SuccessTransaction"] = "Order refund with transaction hash {0} has been processed.",
            ["Plugins.Payments.Xumm.Refund.FailedTransaction"] = "Order refund with transaction hash {0} has failed with code {1}.",
            ["Plugins.Payments.Xumm.Payment.Instruction"] = "Payment for order #{0}",
            ["Plugins.Payments.Xumm.Refund.Instruction"] = "Refund order #{0}",
            ["Plugins.Payments.Xumm.PartialRefund.Instruction"] = "Partial refund order #{0}",
            ["Plugins.Payments.Xumm.Refund.MailDetails"] = "Mail has been sent with refund details. Queued email identifiers: {0}",
            ["Plugins.Payments.Xumm.PaymentMethodDescription"] = "Pay with Xumm",
            ["Plugins.Payments.Xumm.PaymentInfo.IsNotConfigured"] = "Plugin is not configured correctly.",
            ["Plugins.Payments.Xumm.Payment.Successful"] = "We have received your payment. Thanks!"
        });

        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId) ??
                            (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

        if (emailAccount == null)
        {
            throw new Exception("There is no email account to create a MessageTemplate");
        }

        var template = new MessageTemplate
        {
            Name = XummDefaults.Mail.RefundEmailTemplateSystemName,
            Subject = "%Store.Name%. Refund Order #%Order.OrderNumber%",
            Body = $"<p><a href=\"%Store.URL%\">%Store.Name%</a><br /><br />Order #%Order.OrderNumber% refund can be signed <a href=\"%Order.RefundUrl%\" target=\"_blank\">here</a>.<br /><br />Refund amount: %Order.RefundAmount%<br /><br />Date Ordered: %Order.CreatedOn%</p>{Environment.NewLine}",
            IsActive = true,
            EmailAccountId = emailAccount.Id
        };

        await _messageTemplateService.InsertMessageTemplateAsync(template);

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(XummDefaults.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(XummDefaults.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await _settingService.DeleteSettingAsync<XummPaymentSettings>();

        var template = (await _messageTemplateService.GetMessageTemplatesByNameAsync(XummDefaults.Mail.RefundEmailTemplateSystemName)).FirstOrDefault();
        if (template != null)
        {
            await _messageTemplateService.DeleteMessageTemplateAsync(template);
        }

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
    public bool SupportPartiallyRefund => true;

    /// <summary>
    /// Gets a value indicating whether refund is supported
    /// </summary>
    public bool SupportRefund => true;

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

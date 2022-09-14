using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Plugin.Payments.Xumm.Models;
using Nop.Plugin.Payments.Xumm.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System.Globalization;
using System.Threading.Tasks;
using XUMM.NET.SDK.Extensions;

namespace Nop.Plugin.Payments.Xumm.Controllers;

[AutoValidateAntiforgeryToken]
[Area(AreaNames.Admin)]
[AuthorizeAdmin]
public class XummConfigurationController : BasePaymentController
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly IPermissionService _permissionService;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IXummService _xummService;

    #endregion

    #region Ctor

    public XummConfigurationController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext,
        IXummService xummService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
        _xummService = xummService;
    }

    #endregion

    #region Methods

    public async Task<IActionResult> Configure()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
        {
            return AccessDeniedView();
        }

        // Load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<XummPaymentSettings>(storeScope);

        var pong = await _xummService.GetPongAsync();
        var model = new ConfigurationModel
        {
            ActiveStoreScopeConfiguration = storeScope,
            AdditionalFee = settings.AdditionalFee,
            AdditionalFeePercentage = settings.AdditionalFeePercentage,
            ApiKey = settings.ApiKey,
            ApiSecret = settings.ApiSecret,
            WebhookUrl = _xummService.WebhookUrl,
            XrplAddress = settings.XrplAddress,
            XrplPaymentDestinationTag = settings.XrplPaymentDestinationTag?.ToString(),
            XrplRefundDestinationTag = settings.XrplRefundDestinationTag?.ToString(),
            XrplCurrency = IssuerCurrencyExtensions.GetCurrencyIdentifier(settings.XrplIssuer, settings.XrplCurrency),
            ValidXrplAddress = settings.XrplAddress.IsAccountAddress(),
            ValidApiCredentials = pong?.Pong ?? false,
            HasWebhookUrlConfigured = _xummService.HasWebhookUrlConfigured(pong)
        };

        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFeePercentage, storeScope);
            model.XrplAddress_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplAddress, storeScope);
            model.XrplPaymentDestinationTag_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplPaymentDestinationTag, storeScope);
            model.XrplRefundDestinationTag_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplRefundDestinationTag, storeScope);
            model.XrplCurrency_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplCurrency, storeScope);
        }

        if (model.ValidApiCredentials)
        {
            if (!model.HasWebhookUrlConfigured)
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.WebhookUrl.NotConfigured"));
            }

            if (model.ValidXrplAddress)
            {
                var issuers = await _xummService.GetOrderedCurrenciesAsync(settings.XrplAddress);

                foreach (var issuer in issuers)
                {
                    var group = new SelectListGroup
                    {
                        Name = issuer.Name
                    };

                    foreach (var currency in issuer.Currencies)
                    {
                        var isSelected = currency.Identifier == model.XrplCurrency;
                        var listItem = new SelectListItem
                        {
                            Text = currency.CurrencyCodeFormatted,
                            Value = currency.Identifier,
                            Selected = isSelected,
                            Group = group
                        };

                        if (isSelected)
                        {
                            if (currency.TrustSetRequired)
                            {
                                _notificationService.WarningNotification(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplCurrency.MissingTrustLine"), currency.CurrencyCodeFormatted));
                                model.TrustSetRequired = true;
                            }

                            if (!await _xummService.IsPrimaryStoreCurrency(currency.CurrencyCodeFormatted))
                            {
                                _notificationService.WarningNotification(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplCurrency.MissingPrimaryStoreCurrency"), currency.CurrencyCodeFormatted));
                                model.ShopCurrencyRequired = true;
                            }
                        }

                        model.XrplCurrencies.Add(listItem);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(model.XrplAddress) && !model.ValidXrplAddress)
            {
                _notificationService.WarningNotification(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplAddress.Invalid"), model.XrplAddress));
            }
        }

        return View("~/Plugins/Payments.Xumm/Views/Configure.cshtml", model);
    }

    public async Task<IActionResult> ProcessPayloadAsync(int storeScope, string customIdentifier)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
        {
            return AccessDeniedView();
        }

        if (!string.IsNullOrWhiteSpace(customIdentifier))
        {
            await _xummService.ProcessPayloadAsync(storeScope, customIdentifier);
        }

        return RedirectToAction("Configure");
    }

    public async Task<IActionResult> SetAccountWithXummAsync()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var redirectUrl = await _xummService.GetSignInWithXummUrlAsync(storeScope);
        return Redirect(redirectUrl);
    }

    public async Task<IActionResult> SetTrustLineAsync()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<XummPaymentSettings>(storeScope);
        var redirectUrl = await _xummService.GetSetTrustLineUrlAsync(storeScope, settings.XrplAddress, settings.XrplIssuer, settings.XrplCurrency);
        return Redirect(redirectUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
        {
            return AccessDeniedView();
        }

        if (!ModelState.IsValid)
        {
            return await Configure();
        }

        // Load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<XummPaymentSettings>(storeScope);

        var restartRequired = model.ApiKey != settings.ApiKey || model.ApiSecret != settings.ApiSecret;
        if (restartRequired)
        {
            // API Credentials are configured during startup so we need to restart after changed values.
            settings.ApiKey = model.ApiKey;
            settings.ApiSecret = model.ApiSecret;

            await _settingService.SaveSettingAsync(settings, setting => setting.ApiKey, clearCache: false);
            await _settingService.SaveSettingAsync(settings, setting => setting.ApiSecret, clearCache: false);

            return View("~/Areas/Admin/Views/Shared/RestartApplication.cshtml", Url.Action("Configure", "XummConfiguration"));
        }

        if (settings.AdditionalFee != model.AdditionalFee || settings.AdditionalFeePercentage != model.AdditionalFeePercentage)
        {
            settings.AdditionalFee = model.AdditionalFee;
            settings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, clearCache: false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, clearCache: false);
        }

        var xrpAddressChanged = settings.XrplAddress != model.XrplAddress;
        if (xrpAddressChanged)
        {
            settings.XrplAddress = model.XrplAddress;
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplAddress, model.XrplAddress_OverrideForStore, storeScope, false);
        }

        uint? paymentDestinationTag = null;
        if (uint.TryParse(model.XrplPaymentDestinationTag, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedPaymentDestinationTag))
        {
            paymentDestinationTag = parsedPaymentDestinationTag;
        }

        if (settings.XrplPaymentDestinationTag != paymentDestinationTag)
        {
            settings.XrplPaymentDestinationTag = paymentDestinationTag;
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplPaymentDestinationTag, model.XrplPaymentDestinationTag_OverrideForStore, storeScope, false);
        }

        uint? refundDestinationTag = null;
        if (uint.TryParse(model.XrplRefundDestinationTag, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedRefundDestinationTag))
        {
            refundDestinationTag = parsedRefundDestinationTag;
        }

        if (settings.XrplRefundDestinationTag != refundDestinationTag)
        {
            settings.XrplRefundDestinationTag = refundDestinationTag;
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplRefundDestinationTag, model.XrplRefundDestinationTag_OverrideForStore, storeScope, false);
        }

        var (issuer, currency) = IssuerCurrencyExtensions.GetIssuerAndCurrency(model.XrplCurrency);
        var currencyChanged = (issuer != null && issuer != settings.XrplIssuer) || (currency != null && currency != settings.XrplCurrency);
        var disabledCurrencyOverride = issuer == null && currency == null && storeScope > 0;
        if (currencyChanged || disabledCurrencyOverride)
        {
            // Issuer and Currency will only be saved after a trust line has been set to prevent payment failures.
            if (!disabledCurrencyOverride && await _xummService.IsTrustLineRequiredAsync(settings.XrplAddress, issuer, currency))
            {
                // Issuer and Currency will be saved after signing the TrustSet payload.
                var redirectUrl = await _xummService.GetSetTrustLineUrlAsync(storeScope, settings.XrplAddress, issuer, currency);
                return Redirect(redirectUrl);
            }

            settings.XrplIssuer = issuer;
            settings.XrplCurrency = currency;

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplIssuer, model.XrplCurrency_OverrideForStore, storeScope, clearCache: false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplCurrency, model.XrplCurrency_OverrideForStore, storeScope, clearCache: false);
        }
        else if (xrpAddressChanged)
        {
            await _xummService.SetFallBackForMissingTrustLineAsync(settings, storeScope);
        }

        await _settingService.ClearCacheAsync();
        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
        return RedirectToAction("Configure");
    }

    #endregion
}

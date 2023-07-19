using System.Globalization;
using System.Threading.Tasks;
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

        var pong = await _xummService.GetPongAsync(storeScope);
        var model = new ConfigurationModel
        {
            ActiveStoreScopeConfiguration = storeScope,
            AdditionalFee = settings.AdditionalFee,
            AdditionalFeePercentage = settings.AdditionalFeePercentage,
            ApiKey = settings.ApiKey,
            ApiSecret = settings.ApiSecret,
            XrplAddress = settings.XrplAddress,
            XrplPaymentDestinationTag = settings.XrplPaymentDestinationTag?.ToString(),
            XrplRefundDestinationTag = settings.XrplRefundDestinationTag?.ToString(),
            XrplCurrencyAndIssuer = IssuerCurrencyExtensions.GetCurrencyIdentifier(settings.XrplIssuer, settings.XrplCurrency),
            XrplPathfinding = settings.XrplPathfinding,
            XrplPathfindingFallback = settings.XrplPathfindingFallback,
            ValidXrplAddress = settings.XrplAddress.IsAccountAddress(),
            ValidApiCredentials = pong?.Pong ?? false,
            WebhookUrl = await _xummService.GetWebhookUrlAsync(storeScope),
            HasWebhookUrlConfigured = await _xummService.HasWebhookUrlConfiguredAsync(storeScope, pong)
        };

        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AdditionalFeePercentage, storeScope);
            model.ApiKey_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ApiKey, storeScope);
            model.ApiSecret_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ApiSecret, storeScope);
            model.XrplAddress_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplAddress, storeScope);
            model.XrplPaymentDestinationTag_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplPaymentDestinationTag, storeScope);
            model.XrplRefundDestinationTag_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplRefundDestinationTag, storeScope);
            model.XrplCurrencyAndIssuer_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplCurrency, storeScope);
            model.XrplPathfinding_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplPathfinding, storeScope);
            model.XrplPathfindingFallback_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.XrplPathfindingFallback, storeScope);
        }

        if (model.ValidApiCredentials)
        {
            if (!model.HasWebhookUrlConfigured)
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.WebhookUrl.NotConfigured"));
            }

            if (model.ValidXrplAddress)
            {
                var issuers = await _xummService.GetOrderedCurrenciesAsync(storeScope, settings.XrplAddress);

                foreach (var issuer in issuers)
                {
                    var group = new SelectListGroup
                    {
                        Name = issuer.Name
                    };

                    foreach (var currency in issuer.Currencies)
                    {
                        var isSelected = currency.Identifier == model.XrplCurrencyAndIssuer;
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

                            if (!await _xummService.IsPrimaryStoreCurrency(storeScope, currency.CurrencyCodeFormatted))
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

    public async Task<IActionResult> ProcessPayloadAsync(int storeId, string customIdentifier)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
        {
            return AccessDeniedView();
        }

        if (!string.IsNullOrWhiteSpace(customIdentifier))
        {
            await _xummService.ProcessPayloadAsync(storeId, customIdentifier);
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

        settings.ApiKey = model.ApiKey;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.ApiKey, model.ApiKey_OverrideForStore, storeScope, clearCache: false);

        settings.ApiSecret = model.ApiSecret;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.ApiSecret, model.ApiSecret_OverrideForStore, storeScope, clearCache: false);

        settings.AdditionalFee = model.AdditionalFee;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, clearCache: false);

        settings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, clearCache: false);

        settings.XrplAddress = model.XrplAddress;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplAddress, model.XrplAddress_OverrideForStore, storeScope, clearCache: false);

        uint? paymentDestinationTag = null;
        if ((storeScope == 0 || model.XrplPaymentDestinationTag_OverrideForStore) &&
            uint.TryParse(model.XrplPaymentDestinationTag, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedPaymentDestinationTag))
        {
            paymentDestinationTag = parsedPaymentDestinationTag;
        }

        settings.XrplPaymentDestinationTag = paymentDestinationTag;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplPaymentDestinationTag, model.XrplPaymentDestinationTag_OverrideForStore, storeScope, clearCache: false);

        uint? refundDestinationTag = null;
        if ((storeScope == 0 || model.XrplRefundDestinationTag_OverrideForStore) &&
            uint.TryParse(model.XrplRefundDestinationTag, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedRefundDestinationTag))
        {
            refundDestinationTag = parsedRefundDestinationTag;
        }

        settings.XrplRefundDestinationTag = refundDestinationTag;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplRefundDestinationTag, model.XrplRefundDestinationTag_OverrideForStore, storeScope, clearCache: false);

        var (issuer, currency) = storeScope == 0 || model.XrplCurrencyAndIssuer_OverrideForStore ? IssuerCurrencyExtensions.GetIssuerAndCurrency(model.XrplCurrencyAndIssuer) : (null, null);
        settings.XrplIssuer = issuer;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplIssuer, model.XrplCurrencyAndIssuer_OverrideForStore, storeScope, clearCache: false);

        settings.XrplCurrency = currency;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplCurrency, model.XrplCurrencyAndIssuer_OverrideForStore, storeScope, clearCache: false);

        settings.XrplPathfinding = model.XrplPathfinding;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplPathfinding, model.XrplPathfinding_OverrideForStore, storeScope, clearCache: false);

        settings.XrplPathfindingFallback = model.XrplPathfindingFallback;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplPathfindingFallback, model.XrplPathfindingFallback_OverrideForStore, storeScope, clearCache: false);

        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
        return RedirectToAction("Configure");
    }

    #endregion
}

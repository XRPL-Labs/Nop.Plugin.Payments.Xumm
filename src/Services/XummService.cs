using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Plugin.Payments.Xumm.Models;
using Nop.Plugin.Payments.Xumm.WebSocket;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using XUMM.NET.SDK.Clients.Interfaces;
using XUMM.NET.SDK.Enums;
using XUMM.NET.SDK.Extensions;
using XUMM.NET.SDK.Models.Misc;
using XUMM.NET.SDK.Models.Payload;
using XUMM.NET.SDK.Models.Payload.XRPL;
using XUMM.NET.SDK.Models.Payload.Xumm;

namespace Nop.Plugin.Payments.Xumm.Services;

public class XummService : IXummService
{
    #region Fields

    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly ICurrencyService _currencyService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IXrplWebSocket _xrplWebSocket;
    private readonly IXummMiscClient _xummMiscClient;
    private readonly IXummPayloadClient _xummPayloadClient;
    private readonly CurrencySettings _currencySettings;
    private readonly XummPaymentSettings _xummPaymentSettings;
    private readonly ILogger _logger;

    #endregion

    #region Ctor

    public XummService(
        IActionContextAccessor actionContextAccessor,
        ICurrencyService currencyService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService,
        IStoreContext storeContext,
        IUrlHelperFactory urlHelperFactory,
        IXrplWebSocket xrplWebSocket,
        IXummPayloadClient xummPayloadClient,
        IXummMiscClient xummMiscClient,
        CurrencySettings currencySettings,
        XummPaymentSettings xummPaymentSettings,
        ILogger logger)
    {
        _actionContextAccessor = actionContextAccessor;
        _currencyService = currencyService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
        _storeContext = storeContext;
        _urlHelperFactory = urlHelperFactory;
        _xrplWebSocket = xrplWebSocket;
        _xummPayloadClient = xummPayloadClient;
        _xummMiscClient = xummMiscClient;
        _currencySettings = currencySettings;
        _xummPaymentSettings = xummPaymentSettings;
        _logger = logger;
    }

    #endregion

    #region Methods

    public async Task<XummPong> GetPongAsync()
    {
        try
        {
            return await _xummMiscClient.GetPingAsync();
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Failed to retrieve Xumm pong with provided credentials.", ex);
            return null;
        }
    }

    public async Task<string> GetSignInWithXummUrlAsync()
    {
        var payload = new XummPayloadTransaction(XummTransactionType.SignIn).ToXummPostJsonPayload();
        return await GetPayloadRedirectUrlAsync(payload, await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplAddress.SignInWithXummInstruction"));
    }

    public async Task<string> GetSetTrustLineUrlAsync(string account, string issuer, string currency)
    {
        var payload = new XrplTrustSetTransaction(account, currency, issuer, XummDefaults.XRPL.TrustSetValue, XummDefaults.XRPL.Fee)
        {
            Flags = XrplTrustSetFlags.tfSetNoRipple
        }.ToXummPostJsonPayload();

        var instruction = string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplCurrency.SetTrustLineInstruction"), currency.GetFormattedCurrency(), issuer);
        return await GetPayloadRedirectUrlAsync(payload, instruction);
    }

    public async Task<List<IssuerModel>> GetOrderedCurrenciesAsync(string xrpAddress)
    {
        var issuers = new Dictionary<string, IssuerModel>();

        var curatedAssets = await _xummMiscClient.GetCuratedAssetsAsync();
        foreach (var curatedAsset in curatedAssets.Details)
        {
            foreach (var currency in curatedAsset.Value.Currencies.Values)
            {
                if (!issuers.TryGetValue(curatedAsset.Key, out var issuer))
                {
                    issuer = new IssuerModel(curatedAsset.Key);
                    issuers.Add(issuer.Name, issuer);
                }

                issuer.AddCurrency(currency);
            }
        }

        // Order the Curated Assets by name to append the non-curated assets at last
        var result = new List<IssuerModel>(issuers.Values.OrderBy(x => x.Name));

        if (xrpAddress.IsAccountAddress())
        {
            try
            {
                var accountTrustLines = await _xrplWebSocket.GetAccountTrustLines(xrpAddress, true);
                foreach (var trustLine in accountTrustLines)
                {
                    // Trust line of a curated asset
                    var issuer = result.FirstOrDefault(x => x.Currencies.Any(x => x.Account == trustLine.Account && x.CurrencyCode == trustLine.Currency));

                    if (issuer == null)
                    {
                        var header = await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplCurrency.TrustLineHeader");
                        // Grouped non-curated assets by a header key
                        issuer = result.FirstOrDefault(x => x.Name == header);
                        if (issuer == null)
                        {
                            issuer = new IssuerModel(header);
                            result.Add(issuer);
                        }
                    }

                    issuer.AddCurrency(trustLine);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ErrorNotification(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplAddress.TrustLinesFailed"), xrpAddress));
                await _logger.ErrorAsync($"{XummDefaults.SystemName}: {ex.Message}", ex);
            }
        }

        result.ForEach(x => x.Currencies = x.Currencies.OrderBy(c => c.CurrencyCodeFormatted).ToList());

        var xrpIssuer = new IssuerModel(XummDefaults.XRPL.XRP);
        xrpIssuer.Currencies.Add(new CurrencyModel(xrpIssuer)
        {
            CurrencyCode = XummDefaults.XRPL.XRP,
            CurrencyCodeFormatted = XummDefaults.XRPL.XRP
        });

        result.Insert(0, xrpIssuer);

        return result;
    }

    public async Task<(XummPayloadDetails, XummPayloadStatus)> GetPayloadDetailsAsync(string customIdentifier)
    {
        var payload = !string.IsNullOrWhiteSpace(customIdentifier)
            ? await _xummPayloadClient.GetByCustomIdentifierAsync(customIdentifier)
            : null;

        XummPayloadStatus paymentStatus;
        if (payload == null)
        {
            paymentStatus = XummPayloadStatus.NotFound;
        }
        else if (payload.Meta.Resolved && payload.Meta.Signed)
        {
            paymentStatus = XummPayloadStatus.Signed;
        }
        else if (payload.Meta.Resolved)
        {
            paymentStatus = XummPayloadStatus.Rejected;
        }
        else if (!payload.Meta.Resolved && !payload.Meta.Signed && payload.Meta.Cancelled && payload.Meta.Expired)
        {
            paymentStatus = XummPayloadStatus.Cancelled;
        }
        else if (!payload.Meta.Resolved && !payload.Meta.Signed && !payload.Meta.Cancelled && payload.Meta.Expired)
        {
            paymentStatus = XummPayloadStatus.Expired;
        }
        else if (payload.Meta.Resolved && payload.Meta.Signed && payload.Meta.Expired)
        {
            paymentStatus = XummPayloadStatus.ExpiredSigned;
        }
        else
        {
            paymentStatus = XummPayloadStatus.NotInteracted;
        }

        return (payload, paymentStatus);
    }

    public async Task<bool> IsTrustLineRequiredAsync(string xrpAddress, string issuer, string currency)
    {
        var accountTrustLines = await _xrplWebSocket.GetAccountTrustLines(xrpAddress);
        return !currency.Equals(XummDefaults.XRPL.XRP) && !accountTrustLines.Any(x => x.Account.Equals(issuer) && x.Currency.Equals(currency));
    }

    public async Task<bool> IsPrimaryStoreCurrency(string currencyCode)
    {
        var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        return currency?.CurrencyCode == currencyCode;
    }

    public async Task ProcessPayloadAsync(string customIdentifier)
    {
        var (payloadDetails, payloadStatus) = await GetPayloadDetailsAsync(customIdentifier);
        if (payloadStatus != XummPayloadStatus.Signed && payloadStatus != XummPayloadStatus.ExpiredSigned)
        {
            return;
        }

        // Load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<XummPaymentSettings>(storeScope);

        var isConfiguredAddress = payloadDetails.Response.Account.Equals(settings.XrplAddress);
        if (payloadDetails.Payload.TxType.Equals(nameof(XummTransactionType.SignIn)))
        {
            if (!isConfiguredAddress)
            {
                settings.XrplAddress = payloadDetails.Response.Account;

                var overrideForStore = storeScope > 0 && await _settingService.SettingExistsAsync(settings, x => x.XrplAddress, storeScope);
                await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplAddress, overrideForStore, storeScope, false);

                _notificationService.SuccessNotification(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplAddress.Set"), settings.XrplAddress));

                await SetFallBackForMissingTrustLineAsync(settings, storeScope);
                await _settingService.ClearCacheAsync();
            }
        }
        else if (payloadDetails.Payload.TxType.Equals(nameof(XrplTransactionType.TrustSet)))
        {
            if (!isConfiguredAddress)
            {
                _notificationService.ErrorNotification(string.Format(
                    await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplAddress.Wrong"),
                    payloadDetails.Response.Account, settings.XrplAddress));
            }
            else
            {
                var trustSetTransaction = payloadDetails.Payload.RequestJson.Deserialize<XrplTrustSetTransaction>();
                if (trustSetTransaction == null)
                {
                    throw new NopException("Can't deserialize RequestJson of Payload as a XrplTrustSetTransaction.");
                }

                settings.XrplCurrency = trustSetTransaction.LimitAmount.Currency;
                settings.XrplIssuer = trustSetTransaction.LimitAmount.Issuer;

                var overrideForStore = storeScope > 0 && await _settingService.SettingExistsAsync(settings, x => x.XrplCurrency, storeScope);
                await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplCurrency, overrideForStore, storeScope, false);
                await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplIssuer, overrideForStore, storeScope);

                _notificationService.SuccessNotification(
                    string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplCurrency.TrustLineSet"), settings.XrplCurrency, settings.XrplIssuer));
            }
        }
    }

    public async Task<bool> HidePaymentMethodAsync()
    {
        if (!await IsPrimaryStoreCurrency(_xummPaymentSettings.XrplCurrency.GetFormattedCurrency()))
        {
            return true;
        }

        var pong = await GetPongAsync();
        if (!pong?.Pong ?? false)
        {
            return true;
        }

        if (!XummDefaults.WebHooks.AllowUnconfiguredWebhook && !HasWebhookUrlConfigured(pong))
        {
            return true;
        }

        if (await IsTrustLineRequiredAsync(_xummPaymentSettings.XrplAddress, _xummPaymentSettings.XrplIssuer, _xummPaymentSettings.XrplCurrency))
        {
            return true;
        }

        return false;
    }

    public async Task SetFallBackForMissingTrustLineAsync(XummPaymentSettings settings, int storeScope, bool clearCache = false)
    {
        if (settings.XrplIssuer == null || settings.XrplCurrency == null || await IsTrustLineRequiredAsync(settings.XrplAddress, settings.XrplIssuer, settings.XrplCurrency))
        {
            settings.XrplIssuer = XummDefaults.XRPL.XRP;
            settings.XrplCurrency = XummDefaults.XRPL.XRP;

            var currencyOverrideForStore = storeScope > 0 && await _settingService.SettingExistsAsync(settings, x => x.XrplCurrency, storeScope);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplIssuer, currencyOverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.XrplCurrency, currencyOverrideForStore, storeScope, clearCache);

            _notificationService.WarningNotification(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.Xumm.Fields.XrplCurrency.FallBackSet"), XummDefaults.XRPL.XRP));
        }
    }

    public bool HasWebhookUrlConfigured(XummPong pong)
    {
        if (pong?.Auth.Application.WebhookUrl == null)
        {
            return false;
        }

        return WebhookUrl.Equals(pong.Auth.Application.WebhookUrl);
    }

    private async Task<string> GetPayloadRedirectUrlAsync(XummPostJsonPayload payload, string instruction)
    {
        var customIdentifier = Guid.NewGuid().ToString();
        var returnUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.ProcessPayloadRouteName, new { customIdentifier = customIdentifier });

        payload.Options = new XummPayloadOptions
        {
            ReturnUrl = new XummPayloadReturnUrl
            {
                Web = returnUrl
            }
        };
        payload.CustomMeta = new XummPayloadCustomMeta
        {
            Instruction = instruction,
            Identifier = customIdentifier
        };

        var result = await _xummPayloadClient.CreateAsync(payload, true);
        if (result == null)
        {
            throw new NopException("Failed to get Xumm payload response.");
        }

        return result.Next.Always;
    }

    #endregion

    #region Properties

    public string WebhookUrl => _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).Link(XummDefaults.WebHooks.RouteName, null);

    #endregion
}

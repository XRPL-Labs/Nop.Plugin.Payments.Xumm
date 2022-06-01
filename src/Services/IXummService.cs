using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Models;
using XUMM.NET.SDK.Models.Misc;
using XUMM.NET.SDK.Models.Payload;

namespace Nop.Plugin.Payments.Xumm.Services;

public interface IXummService
{
    bool HasWebhookUrlConfigured(XummPong pong);
    Task<XummPong> GetPongAsync();
    Task ProcessPayloadAsync(string xummId);
    Task<string> GetSignInWithXummUrlAsync();
    Task<string> GetSetTrustLineUrlAsync(string account, string issuer, string currency);
    Task<bool> IsTrustLineRequiredAsync(string xrpAddress, string issuer, string currency);
    Task<List<IssuerModel>> GetOrderedCurrenciesAsync(string xrpAddress);
    Task<bool> IsPrimaryStoreCurrency(string currencyCode);
    Task<bool> HidePaymentMethodAsync();
    Task SetFallBackForMissingTrustLineAsync(XummPaymentSettings settings, int storeScope, bool clearCache = false);
    Task<(XummPayloadDetails, XummPayloadStatus)> GetPayloadDetailsAsync(string customIdentifier);

    string WebhookUrl { get; }
}

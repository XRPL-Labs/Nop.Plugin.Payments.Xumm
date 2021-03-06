using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Models;
using XUMM.NET.SDK.Models.Misc;
using XUMM.NET.SDK.Models.Payload;

namespace Nop.Plugin.Payments.Xumm.Services;

public interface IXummService
{
    Task<XummPong> GetPongAsync();
    Task<string> GetSignInWithXummUrlAsync();
    Task<string> GetSetTrustLineUrlAsync(string account, string issuer, string currency);
    Task<List<IssuerModel>> GetOrderedCurrenciesAsync(string xrpAddress);
    Task<(XummPayloadDetails, XummPayloadStatus)> GetPayloadDetailsAsync(string customIdentifier);
    Task<bool> IsTrustLineRequiredAsync(string xrpAddress, string issuer, string currency);
    Task<bool> IsPrimaryStoreCurrency(string currencyCode);
    Task ProcessPayloadAsync(string xummId);
    Task<bool> HidePaymentMethodAsync();
    Task SetFallBackForMissingTrustLineAsync(XummPaymentSettings settings, int storeScope, bool clearCache = false);
    bool HasWebhookUrlConfigured(XummPong pong);
    string WebhookUrl { get; }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.Enums;
using Nop.Plugin.Payments.Xumm.Models;
using XUMM.NET.SDK;
using XUMM.NET.SDK.Models.Misc;
using XUMM.NET.SDK.Models.Payload;

namespace Nop.Plugin.Payments.Xumm.Services;

public interface IXummService
{
    Task<XummSdk> GetXummSdk(int storeId);
    Task<XummPong> GetPongAsync(int storeId);
    Task<string> GetSignInWithXummUrlAsync(int storeId);
    Task<string> GetSetTrustLineUrlAsync(int storeId, string account, string issuer, string currency);
    Task<List<IssuerModel>> GetOrderedCurrenciesAsync(int storeId, string xrpAddress);
    Task<(XummPayloadDetails, XummPayloadStatus)> GetPayloadDetailsAsync(int storeId, string customIdentifier);
    Task<bool> IsTrustLineRequiredAsync(string xrpAddress, string issuer, string currency);
    Task<bool> IsPrimaryStoreCurrency(int storeId, string currencyCode);
    Task ProcessPayloadAsync(int storeId, string xummId);
    Task<bool> HidePaymentMethodAsync(int storeId);
    Task SetFallBackForMissingTrustLineAsync(XummPaymentSettings settings, int storeId, bool clearCache = false);
    Task<bool> HasWebhookUrlConfiguredAsync(int storeId, XummPong pong);
    Task<string> GetWebhookUrlAsync(int storeId);
}

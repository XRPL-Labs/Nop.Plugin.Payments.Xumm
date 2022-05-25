using Nop.Plugin.Payments.Xumm.Extensions;

namespace Nop.Plugin.Payments.Xumm.Models;

public class CurrencyModel
{
    public CurrencyModel(IssuerModel issuer)
    {
        Issuer = issuer;
    }

    public string Identifier => IssuerCurrencyExtensions.GetCurrencyIdentifier(Account, CurrencyCode);
    public string Account { get; set; }
    public string CurrencyCode { get; set; }
    public string CurrencyCodeFormatted { get; set; }
    public bool TrustSetRequired { get; set; }
    public IssuerModel Issuer { get; }
}

using System.Collections.Generic;
using Nop.Plugin.Payments.Xumm.Extensions;
using Nop.Plugin.Payments.Xumm.WebSocket.Models;
using XUMM.NET.SDK.Models.Misc;

namespace Nop.Plugin.Payments.Xumm.Models;

public class IssuerModel
{
    public IssuerModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public List<CurrencyModel> Currencies { get; set; } = new();

    public void AddCurrency(XummCuratedAssetsDetailsCurrency currency)
    {
        if (Exists(currency.Currency))
        {
            return;
        }

        Currencies.Add(new CurrencyModel(this)
        {
            Account = currency.Issuer,
            CurrencyCode = currency.Currency,
            CurrencyCodeFormatted = currency.CurrencyFormatted,
            TrustSetRequired = true
        });
    }

    public void AddCurrency(AccountTrustLine accountTrustLine)
    {
        if (Exists(accountTrustLine.Currency, true))
        {
            return;
        }

        Currencies.Add(new CurrencyModel(this)
        {
            Account = accountTrustLine.Account,
            CurrencyCode = accountTrustLine.Currency,
            CurrencyCodeFormatted = $"{accountTrustLine.Currency.GetFormattedCurrency()} ({accountTrustLine.Account})",
            TrustSetRequired = false
        });
    }

    private bool Exists(string currencyCode, bool trustLineSet = false)
    {
        var idx = Currencies.FindIndex(x => x.CurrencyCode == currencyCode);
        if (idx == -1)
        {
            return false;
        }

        if (trustLineSet)
        {
            Currencies[idx].TrustSetRequired = false;
        }

        return true;
    }
}

using System;
using XUMM.NET.SDK.Extensions;

namespace Nop.Plugin.Payments.Xumm.Extensions;

internal static class IssuerCurrencyExtensions
{
    internal static string GetFormattedCurrency(this string currency)
    {
        if (currency == XummDefaults.XRPL.XRP)
        {
            return currency;
        }

        return currency.ToFormattedCurrency();
    }

    internal static string GetCurrencyIdentifier(string issuer, string currency)
    {
        if (issuer == null && currency == null)
        {
            return null;
        }

        return $"{issuer ?? currency}-{currency}";
    }

    internal static (string issuer, string currency) GetIssuerAndCurrency(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return (null, null);
        }

        var split = identifier.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length != 2)
        {
            throw new ArgumentException("Identifier {identifier} doesn't contain a valid account and currency code.");
        }

        return (split[0], split[1]);
    }
}

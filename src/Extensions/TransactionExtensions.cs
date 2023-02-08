using System;
using System.Collections.Generic;
using System.Linq;
using XUMM.NET.SDK.Models.Misc;

namespace Nop.Plugin.Payments.Xumm.Extensions;

internal static class TransactionExtensions
{
    internal static bool IsEqualTo(this XummTransactionBalanceChanges value1, XummTransactionBalanceChanges value2)
    {
        if (string.IsNullOrWhiteSpace(value1.CounterParty) && string.IsNullOrWhiteSpace(value2.CounterParty))
        {
            // XRP
            return true;
        }
        
        if (!value1.CounterParty?.Equals(value2.CounterParty) ?? false)
        {
            return false;
        }

        return value1.Currency.Equals(value2.Currency);
    }

    internal static List<XummTransactionBalanceChanges> GetReceivedBalanceChanges(this XummTransaction xummTransaction, string account)
    {
        return xummTransaction.GetBalanceChanges(account, false);
    }

    internal static List<XummTransactionBalanceChanges> GetDeductedBalanceChanges(this XummTransaction xummTransaction, string account)
    {
        return xummTransaction.GetBalanceChanges(account, true);
    }

    private static List<XummTransactionBalanceChanges> GetBalanceChanges(this XummTransaction xummTransaction, string account, bool deducted)
    {
        var unfiltered = new List<XummTransactionBalanceChanges>();

        if (xummTransaction.BalanceChanges.TryGetValue(account, out var changes))
        {
            foreach (var change in changes)
            {
                var firstCharacter = change.Value.Length > 0 ? change.Value[..1] : string.Empty;
                var isDeduction = firstCharacter.Equals("-", StringComparison.OrdinalIgnoreCase);

                if (deducted && isDeduction)
                {
                    unfiltered.Add(change);
                }
                else if (!deducted && !isDeduction)
                {
                    unfiltered.Add(change);
                }
            }
        }

        // Filter out XRP if XRP is used for fee only
        var filtered = unfiltered.Where(x => !x.Currency.Equals(XummDefaults.XRPL.XRP) && !string.IsNullOrEmpty(x.CounterParty)).ToList();
        return filtered.Count != 0 ? filtered : unfiltered;
    }
}

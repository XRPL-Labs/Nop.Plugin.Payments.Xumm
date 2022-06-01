using System;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.Xumm.Extensions
{
    internal static class OrderExtensions
    {
        public static string GetCustomIdentifier(this Order order, int attempt) => $"{order.OrderGuid}-{attempt}";

        public static (Guid orderGuid, int attempt) ParseCustomIdentifier(string customIdentifier)
        {
            var lastIndex = customIdentifier?.LastIndexOf('-') ?? -1;
            if (lastIndex == -1)
            {
                return (default, default);
            }

            if (Guid.TryParse(customIdentifier[..lastIndex], out var orderGuid) &&
                int.TryParse(customIdentifier[(lastIndex + 1)..], out var attempt))
            {
                return (orderGuid, attempt);
            }

            return (default, default);
        }
    }
}

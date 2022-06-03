using System;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Xumm.Enums;

namespace Nop.Plugin.Payments.Xumm.Extensions
{
    internal static class OrderExtensions
    {
        public static string GetCustomIdentifier(this Order order, XummPayloadType payloadType, int count)
        {
            return $"{order.OrderGuid}-{(int)payloadType}-{count}";
        }

        public static (Guid orderGuid, XummPayloadType payloadType, int attempt) ParseCustomIdentifier(string customIdentifier)
        {
            var countIndex = customIdentifier?.LastIndexOf('-') ?? -1;
            var typeIndex = countIndex != -1 ? customIdentifier.LastIndexOf('-', countIndex) : -1;
            if (countIndex == -1 || typeIndex == -1)
            {
                return (default, default, default);
            }

            if (Guid.TryParse(customIdentifier[..typeIndex], out var orderGuid) &&
                int.TryParse(customIdentifier.Substring(typeIndex + 1, countIndex - (typeIndex +1)), out var type) &&
                int.TryParse(customIdentifier[(countIndex + 1)..], out var count))
            {
                return (orderGuid, (XummPayloadType)type, count);
            }

            return (default, default, default);
        }
    }
}

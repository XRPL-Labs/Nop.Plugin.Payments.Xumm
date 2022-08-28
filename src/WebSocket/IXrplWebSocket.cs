using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.WebSocket.Models;
using XUMM.NET.SDK.Models.Payload.XRPL;

namespace Nop.Plugin.Payments.Xumm.WebSocket;

public interface IXrplWebSocket
{
    Task<List<AccountTrustLine>> GetAccountTrustLines(string account, bool throwError = false);
    Task<(decimal?,XrplPaymentPathSpecification[][]?)> GetDestinationAmountAndPathsAsync(PathFindRequest pathFindRequest, bool hasCounterParty);
}

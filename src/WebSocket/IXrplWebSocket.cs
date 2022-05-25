using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.WebSocket.Models;

namespace Nop.Plugin.Payments.Xumm.WebSocket;

public interface IXrplWebSocket
{
    Task<List<AccountTrustLine>> GetAccountTrustLines(string account, bool throwError = false);
}

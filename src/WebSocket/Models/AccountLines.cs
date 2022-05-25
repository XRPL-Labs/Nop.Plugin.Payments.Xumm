using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class AccountLines
{
    [JsonPropertyName("account")]
    public string Account { get; set; }

    [JsonPropertyName("lines")]
    public List<AccountTrustLine> TrustLines { get; set; }

    [JsonPropertyName("ledger_current_index")]
    public uint? LedgerCurrentIndex { get; set; }

    [JsonPropertyName("ledger_index")]
    public uint? LedgerIndex { get; set; }

    [JsonPropertyName("ledger_hash")]
    public string LedgerHash { get; set; }

    [JsonPropertyName("marker")]
    public object Marker { get; set; }
}

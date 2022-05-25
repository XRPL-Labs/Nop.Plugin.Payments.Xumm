using System.Text.Json.Serialization;
using Nop.Plugin.Payments.Xumm.WebSocket.Enums;

namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class AccountLinesRequest : BaseRequest
{
    public AccountLinesRequest(string account) : base("account_lines")
    {
        Account = account;
    }

    /// <summary>
    /// A unique identifier for the account, most commonly the account's Address.
    /// </summary>
    [JsonPropertyName("account")]
    public string Account { get; set; }

    /// <summary>
    /// The ledger index of the ledger to use, or a shortcut string to choose a ledger automatically. (See Specifying Ledgers)
    /// </summary>
    [JsonPropertyName("ledger_index")]
    public LedgerIndexType? LedgerIndexType { get; set; }

    /// <summary>
    /// The Address of a second account. If provided, show only lines of trust connecting the two accounts.
    /// </summary>
    [JsonPropertyName("peer")]
    public string? Peer { get; set; }

    /// <summary>
    /// Limit the number of trust lines to retrieve. The server is not required to honor this value. Must be within the inclusive range 10 to 400.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Value from a previous paginated response. Resume retrieving data where that response left off. 
    /// </summary>
    [JsonPropertyName("marker")]
    public object? Marker { get; set; }
}

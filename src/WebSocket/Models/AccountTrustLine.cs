using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class AccountTrustLine
{
    [JsonPropertyName("account")]
    public string Account { get; set; }

    [JsonPropertyName("balance")]
    public string Balance { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("limit")]
    public string Limit { get; set; }

    [JsonPropertyName("limit_peer")]
    public string LimitPeer { get; set; }

    [JsonPropertyName("quality_in")]
    public uint QualityIn { get; set; }

    [JsonPropertyName("quality_out")]
    public uint QualityOut { get; set; }

    [JsonPropertyName("no_ripple")]
    public bool? NoRipple { get; set; }

    [JsonPropertyName("no_ripple_peer")]
    public bool? NoRipplePeer { get; set; }

    [JsonPropertyName("authorized")]
    public bool? Authorized { get; set; }

    [JsonPropertyName("Peer_authorized")]
    public bool? PeerAuthorized { get; set; }

    [JsonPropertyName("freeze")]
    public bool? Freeze { get; set; }

    [JsonPropertyName("freeze_peer")]
    public bool? FreezePeer { get; set; }
}

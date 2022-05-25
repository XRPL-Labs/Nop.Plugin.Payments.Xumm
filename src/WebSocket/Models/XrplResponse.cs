using System;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class XrplResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("result")]
    public object Result { get; set; }
}

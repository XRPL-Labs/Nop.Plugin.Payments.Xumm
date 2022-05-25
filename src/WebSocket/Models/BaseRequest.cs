using System;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class BaseRequest
{
    public BaseRequest(string command)
    {
        Id = Guid.NewGuid();
        Command = command;
    }

    [JsonPropertyName("id")]
    public Guid Id { get; }

    [JsonPropertyName("command")]
    public string Command { get; }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Nop.Plugin.Payments.Xumm.WebSocket.Enums;
using Nop.Plugin.Payments.Xumm.WebSocket.Models;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.Xumm.WebSocket;

public class XrplWebSocket : IXrplWebSocket
{
    private const int CHUNK_SIZE = 1024;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    public XrplWebSocket(ILogger logger)
    {
        _logger = logger;

        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }

    public async Task<List<AccountTrustLine>> GetAccountTrustLines(string account, bool throwError = false)
    {
        var result = new List<AccountTrustLine>();
        try
        {
            object? marker = null;
            do
            {
                var accountLines = await GetAccountLinesAsync(account, marker);
                result.AddRange(accountLines.TrustLines);
                marker = accountLines.Marker;
            } while (marker != null);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Failed to retrieve all account trust lines of account {account}.", ex);

            if (throwError)
            {
                throw;
            }
        }

        return result;
    }

    private async Task<AccountLines> GetAccountLinesAsync(string account, object? marker = null)
    {
        try
        {
            var request = new AccountLinesRequest(account)
            {
                LedgerIndexType = LedgerIndexType.Validated,
                Marker = marker
            };

            return await SendMessageAsync<AccountLines>(Defaults.WebSocket.FullHistoryCluster, request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Failed to retrieve account lines of {account}.", ex);
            throw;
        }
    }

    private async Task<T> SendMessageAsync<T>(Uri uri, object request, CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var webSocket = new ClientWebSocket();
        await webSocket.ConnectAsync(uri, cancellationToken);

        if (webSocket.State != WebSocketState.Open)
        {
            return null;
        }

        var message = JsonSerializer.SerializeToUtf8Bytes(request, _serializerOptions);
        var messagesCount = (int)Math.Ceiling((double)message.Length / CHUNK_SIZE);

        for (var i = 0; i < messagesCount; i++)
        {
            var offset = CHUNK_SIZE * i;
            var count = CHUNK_SIZE;
            var lastMessage = i + 1 == messagesCount;

            if (count * (i + 1) > message.Length)
            {
                count = message.Length - offset;
            }

            await webSocket.SendAsync(new ArraySegment<byte>(message, offset, count), WebSocketMessageType.Text, lastMessage, cancellationToken);
        }

        var buffer = new ArraySegment<byte>(new byte[CHUNK_SIZE]);

        await using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            ms.Write(buffer.Array!, buffer.Offset, result.Count);
        } while (!result.EndOfMessage && !cancellationToken.IsCancellationRequested);

        ms.Seek(0, SeekOrigin.Begin);

        var response = JsonSerializer.Deserialize<XrplResponse>(ms)!;
        if (response.Status == "error")
        {
            throw new Exception(response.Error);
        }

        return JsonSerializer.Deserialize<T>(response.Result.ToString()!);
    }
}

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
using XUMM.NET.SDK.Extensions;
using XUMM.NET.SDK.Models.Payload.XRPL;

namespace Nop.Plugin.Payments.Xumm.WebSocket;

public class XrplWebSocket : IXrplWebSocket
{
    #region Fields

    private const int CHUNK_SIZE = 1024;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    #endregion

    #region Ctor

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

    #endregion

    #region Methods

    public async Task<(decimal?, XrplPaymentPathSpecification[][]?)> GetDestinationAmountAndPathsAsync(PathFindCreateRequest pathFindRequest, bool hasCounterParty)
    {
        decimal? destinationAmount = null;
        XrplPaymentPathSpecification[][]? paths = null;

        try
        {
            var source = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            source.Token.ThrowIfCancellationRequested();

            using var webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(XummDefaults.WebSocket.Cluster, source.Token);

            await SendMessageAsync(webSocket, pathFindRequest);

            var buffer = new ArraySegment<byte>(new byte[1024]);

            while (webSocket.State == WebSocketState.Open)
            {
                await using var ms = new MemoryStream();
                WebSocketReceiveResult? result;

                try
                {
                    do
                    {
                        result = await webSocket.ReceiveAsync(buffer, source.Token);
                        ms.Write(buffer.Array!, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage && !source.Token.IsCancellationRequested);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                ms.Seek(0, SeekOrigin.Begin);
                ms.Position = 0;

                var jsonElement = JsonSerializer.Deserialize<JsonElement>(ms);

                // Wait until we receive the best path for the current ledger
                // https://xrpl.org/path_find.html#asynchronous-follow-ups
                if (!jsonElement.TryGetProperty("full_reply", out var fullReplyElement) || !fullReplyElement.GetBoolean() ||
                    !jsonElement.TryGetProperty("alternatives", out var alternativesElement))
                {
                    continue;
                }

                foreach (var alternative in alternativesElement.EnumerateArray())
                {
                    if (alternative.TryGetProperty("paths_computed", out var pathsComputedElement))
                    {
                        paths = pathsComputedElement.Deserialize<XrplPaymentPathSpecification[][]>();

                        if (paths == null)
                        {
                            continue;
                        }

                        if (alternative.TryGetProperty("destination_amount", out var destinationAmountElement))
                        {
                            if (hasCounterParty)
                            {
                                var currencyAmount = destinationAmountElement.Deserialize<XrplTransactionCurrencyAmount>();
                                destinationAmount = currencyAmount?.Value.XrplStringNumberToDecimal();
                            }
                            else
                            {
                                var currencyAmount = destinationAmountElement.GetString();
                                if (currencyAmount != null)
                                {
                                    destinationAmount = currencyAmount.XrpDropsToDecimal();
                                }
                            }
                        }

                        if (destinationAmount == null)
                        {
                            // Couldn't determine the amount to deliver for the found paths
                            paths = null;
                        }
                        else
                        {
                            await SendMessageAsync(webSocket, new PathFindCloseRequest());

                            source.Cancel();
                            break;
                        }
                    }
                }
            }

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Finished fetching paths", source.Token);
            }

            return (destinationAmount, paths);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: .", ex);
            throw;
        }
    }

    public async Task<List<AccountTrustLine>> GetAccountTrustLines(string account, bool throwError = false)
    {
        var result = new List<AccountTrustLine>();
        try
        {
            var source = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            source.Token.ThrowIfCancellationRequested();

            using var webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(XummDefaults.WebSocket.Cluster, source.Token);
            if (webSocket.State != WebSocketState.Open)
            {
                return result;
            }

            object marker = null;
            do
            {
                var accountLines = await GetAccountLinesAsync(webSocket, source.Token, account, marker);
                result.AddRange(accountLines.TrustLines);
                marker = accountLines.Marker;
            } while (marker != null);

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Finished fetching account lines", source.Token);
            }
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: Failed to retrieve all account trust lines of account {account}.", ex);

            if (throwError)
            {
                throw;
            }
        }

        return result;
    }

    private async Task<AccountLines> GetAccountLinesAsync(ClientWebSocket webSocket, CancellationToken cancellationToken, string account, object marker = null)
    {
        try
        {
            var request = new AccountLinesRequest(account)
            {
                LedgerIndexType = LedgerIndexType.Validated,
                Marker = marker
            };

            await SendMessageAsync(webSocket, request);

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

            return JsonSerializer.Deserialize<AccountLines>(response.Result.ToString()!);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"{XummDefaults.SystemName}: Failed to retrieve account lines of {account}.", ex);
            throw;
        }
    }

    private async Task SendMessageAsync(ClientWebSocket clientWebSocket, object request)
    {
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

            await clientWebSocket.SendAsync(new ArraySegment<byte>(message, offset, count), WebSocketMessageType.Text, lastMessage, CancellationToken.None);
        }
    }

    #endregion
}

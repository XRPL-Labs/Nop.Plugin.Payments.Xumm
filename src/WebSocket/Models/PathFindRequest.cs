using System.Globalization;
using System.Text.Json.Serialization;
using XUMM.NET.SDK.Extensions;
using XUMM.NET.SDK.Models.Payload.XRPL;

namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class PathFindRequest : BaseRequest
{
    public PathFindRequest() : base("path_find")
    {
        SubCommand = "create";
    }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("source_account")]
    public string SourceAccount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("destination_account")]
    public string DestinationAccount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonPropertyName("destination_amount")]
    public object? DestinationAmount { get; set; }

    [JsonPropertyName("send_max")]
    public object? SendMax { get; set; }

    public void SetDestinationToXrp()
    {
        DestinationAmount = "-1";
    }

    public void SetDestinationAmountToCounterParty(string currency, string issuer)
    {
        DestinationAmount = new XrplTransactionCurrencyAmount
        {
            Currency = currency,
            Value = "-1",
            Issuer = issuer
        };
    }
    public void SetSendMaxAmount(decimal amount)
    {
        SendMax = amount.XrpToDropsString();
    }

    public void SetSendMaxAmount(string currency, decimal amount, string issuer)
    {
        SendMax = new XrplTransactionCurrencyAmount
        {
            Currency = currency,
            Value = amount.ToString(CultureInfo.InvariantCulture),
            Issuer = issuer
        };
    }
}
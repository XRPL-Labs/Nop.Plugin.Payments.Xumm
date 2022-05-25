namespace Nop.Plugin.Payments.Xumm.Models;

/// <summary>
/// Represents a payment info model
/// </summary>
public record PaymentInfoModel
{
    public string AppStoreUrl { get; set; }

    public string GooglePlayUrl { get; set; }
}

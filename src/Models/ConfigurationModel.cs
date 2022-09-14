using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.Xumm.Models;

/// <summary>
/// Represents a plugin configuration model
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    #region Properties

    public int ActiveStoreScopeConfiguration { get; set; }

    /// <summary>
    /// Gets or sets an API Key
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.ApiKey")]
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets an API Secret
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.ApiSecret")]
    public string ApiSecret { get; set; }

    public bool ValidApiCredentials { get; set; }

    /// <summary>
    /// Gets or sets a WebhookUrl
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.WebhookUrl")]
    public string WebhookUrl { get; set; }

    public bool HasWebhookUrlConfigured { get; set; }

    /// <summary>
    /// Gets or sets an XRPL Address
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.XrplAddress")]
    public string XrplAddress { get; set; }

    public bool XrplAddress_OverrideForStore { get; set; }

    public bool ValidXrplAddress { get; set; }

    /// <summary>
    /// Gets or sets an XRPL Payment Destination Tag
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.XrplPaymentDestinationTag")]
    public string XrplPaymentDestinationTag { get; set; }

    public bool XrplPaymentDestinationTag_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets an XRPL Refund Destination Tag
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.XrplRefundDestinationTag")]
    public string XrplRefundDestinationTag { get; set; }

    public bool XrplRefundDestinationTag_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets an XRPL Currency
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.XrplCurrency")]
    public string XrplCurrency { get; set; }

    public bool XrplCurrency_OverrideForStore { get; set; }

    public IList<SelectListItem> XrplCurrencies { get; set; } = new List<SelectListItem>();

    public bool TrustSetRequired { get; set; }
    
    public bool ShopCurrencyRequired { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }

    public bool AdditionalFee_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false -
    /// fixed value.
    /// </summary>
    [NopResourceDisplayName("Plugins.Payments.Xumm.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }

    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    #endregion
}

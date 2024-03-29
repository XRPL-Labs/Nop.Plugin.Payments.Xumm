﻿using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Xumm;

/// <summary>
/// Represents plugin settings
/// </summary>
public class XummPaymentSettings : ISettings
{
    #region Properties

    /// <summary>
    /// API Key which can be obtained from the Xumm Developer Console
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// API Secret which can be obtained from the Xumm Developer Console
    /// </summary>
    public string ApiSecret { get; set; }

    /// <summary>
    /// Your XRPL Address
    /// </summary>
    public string XrplAddress { get; set; }

    /// <summary>
    /// The XRPL Payment Destination Tag
    /// </summary>
    public uint? XrplPaymentDestinationTag { get; set; }

    /// <summary>
    /// The XRPL Refund Destination Tag
    /// </summary>
    public uint? XrplRefundDestinationTag { get; set; }

    /// <summary>
    /// The currency you will be paid in
    /// </summary>
    public string XrplCurrency { get; set; }

    /// <summary>
    /// The issuer of the currency
    /// </summary>
    public string XrplIssuer { get; set; }

    /// <summary>
    /// Pay with any token available on the XRPL with pathfinding enabled
    /// </summary>
    public bool XrplPathfinding { get; set; }

    /// <summary>
    /// Pathfinding fallback enables older Xumm clients (version &lt; 2.4.0) to process payments using a native 1:1 asset exchange instead of the modern pathfinding UX.
    /// </summary>
    public bool XrplPathfindingFallback { get; set; }

    /// <summary>
    /// Gets or sets a additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false -
    /// fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    #endregion
}

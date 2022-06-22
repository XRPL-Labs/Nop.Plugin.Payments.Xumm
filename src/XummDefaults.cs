using System;
using Nop.Core.Caching;

namespace Nop.Plugin.Payments.Xumm;

/// <summary>
/// Represents Xumm plugin defaults
/// </summary>
public static class XummDefaults
{
    /// <summary>
    /// Gets a name of the view component to display payment info in public store
    /// </summary>
    public const string PAYMENT_INFO_VIEW_COMPONENT_NAME = "XummPaymentInfo";

    /// <summary>
    /// Gets the plugin system name
    /// </summary>
    public static string SystemName => "Payments.Xumm";

    public static string FullSystemName => "Plugin.Payments.Xumm";

    /// <summary>
    /// Gets the plugin configuration route name
    /// </summary>
    public static string ConfigurationRouteName => $"{FullSystemName}.Configure";

    /// <summary>
    /// Gets the plugin route name to process payloads
    /// </summary>
    public static string ProcessPayloadRouteName => $"{FullSystemName}.ProcessPayload";

    /// <summary>
    /// Gets the plugin payment processor handler route name
    /// </summary>
    public static string PaymentProcessorRouteName => $"{FullSystemName}.PaymentProcessor";

    /// <summary>
    /// Gets the plugin refund processor handler route name
    /// </summary>
    public static string RefundProcessorRouteName => $"{FullSystemName}.Refundrocessor";

    /// <summary>
    /// Gets the name of a generic attribute to store the attempt of an order payload type
    /// </summary>
    public static string OrderPayloadCountAttributeName => "XummOrder{0}PayloadCount";

    /// <summary>
    /// Gets the name of a generic attribute to store the processed order payload counts
    /// </summary>
    public static string OrderPayloadCountProcessedAttributeName => "XummOrder{0}PayloadCountProcessed";

    /// <summary>
    /// Gets the <see cref="CacheKey"/> for the list of valid amounts to refund per order
    /// </summary>
    public static CacheKey RefundCacheKey => new($"{FullSystemName}-{{0}}", FullSystemName);

    public static class XRPL
    {
        /// <summary>
        /// Get the default currency if none has been selected
        /// </summary>
        public static string XRP => "XRP";

        /// <summary>
        /// Integer amount of XRP, in drops, to be destroyed as a cost for distributing this transaction to the network.
        /// </summary>
        public static int Fee => 12;

        /// <summary>
        /// Quoted decimal representation of the limit to set on the trust line.
        /// </summary>
        public static string TrustSetValue => "1000";

        /// <summary>
        /// The rippled server summarizes transaction results with result codes, which appear in  meta.TransactionResult.
        /// <seealso href="https://xrpl.org/transaction-results.html"/>
        /// </summary>
        public static string SuccesTransactionResultPrefix => "tes";
    }

    public static class Mail
    {
        /// <summary>
        /// Gets the systemname of the refund email template
        /// </summary>
        public static string RefundEmailTemplateSystemName => $"{SystemName}.RefundMessage";
    }

    /// <summary>
    /// Represents a web hooks defaults
    /// </summary>
    public static class WebHooks
    {
        /// <summary>
        /// Gets the route name
        /// </summary>
        public static string RouteName => $"{FullSystemName}.WebHook.Handle";

        /// <summary>
        /// Allowing any Webhook URL should be enabled for testing purposes only.
        /// Xumm payment method will be enabled even if the shops Webhook URL hasn't been configured in the Xumm Developer Console.
        /// Enabling this feature could cause orders not being marked as paid/cancelled.
        /// </summary>
        public static bool AllowUnconfiguredWebhook => false;
    }

    /// <summary>
    /// Represents WebSocket defaults
    /// </summary>
    public static class WebSocket
    {
        /// <summary>
        /// Gets the API base URL
        /// </summary>
        public static Uri Cluster => new("wss://xrplcluster.com");
    }
}

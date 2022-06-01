using System;

namespace Nop.Plugin.Payments.Xumm;

/// <summary>
/// Represents plugin defaults
/// </summary>
public static class Defaults
{
    /// <summary>
    /// Gets a name of the view component to display payment info in public store
    /// </summary>
    public const string PAYMENT_INFO_VIEW_COMPONENT_NAME = "XummPaymentInfo";

    /// <summary>
    /// Gets the plugin system name
    /// </summary>
    public static string SystemName => "Payments.Xumm";

    /// <summary>
    /// Gets the plugin configuration route name
    /// </summary>
    public static string ConfigurationRouteName => "Plugin.Payments.Xumm.Configure";

    /// <summary>
    /// Gets the plugin route name to process payloads
    /// </summary>
    public static string ProcessPayloadRouteName => "Plugin.Payments.Xumm.ProcessPayload";

    /// <summary>
    /// Gets the plugin payment processor handler route name
    /// </summary>
    public static string PaymentProcessorRouteName => "Plugin.Payments.Xumm.PaymentProcessor";

    /// <summary>
    /// Gets the name of a generic attribute to store the attempt of an order payment
    /// </summary>
    public static string OrderPaymentAttemptAttributeName => "XummOrderPaymentAttempt";

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

    /// <summary>
    /// Represents a web hooks defaults
    /// </summary>
    public static class WebHooks
    {
        /// <summary>
        /// Gets the route name
        /// </summary>
        public static string RouteName => "Plugin.Payments.Xumm.WebHook.Handle";

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

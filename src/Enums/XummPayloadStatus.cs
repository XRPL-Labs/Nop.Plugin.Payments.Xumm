using System.ComponentModel;

namespace Nop.Plugin.Payments.Xumm.Enums
{
    public enum XummPayloadStatus
    {
        [Description("Sign request cannot be found or belongs to another application (API credentials).")]
        NotFound,

        [Description("Sign request has been resolved by the user by signing the sign request.")]
        Signed,

        [Description("Sign request has been resolved by the user by rejecting the sign request.")]
        Rejected,

        [Description("Sign request has been cancelled, user didn't interact.")]
        Cancelled,

        [Description("Sign request expired, user didn't interact.")]
        Expired,

        [Description("Sign request expired, but user opened before expiration and resolved by signing after expiration.")]
        ExpiredSigned,

        [Description("User did not interact with the QR code.")]
        NotInteracted
    }
}

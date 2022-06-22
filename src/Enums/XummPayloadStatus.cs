namespace Nop.Plugin.Payments.Xumm.Enums;

public enum XummPayloadStatus
{
    NotFound,
    Signed,
    Rejected,
    Cancelled,
    Expired,
    ExpiredSigned,
    NotInteracted
}

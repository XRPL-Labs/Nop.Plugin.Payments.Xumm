namespace Nop.Plugin.Payments.Xumm.WebSocket.Models;

public class PathFindCloseRequest : BaseRequest
{
    public PathFindCloseRequest() : base("path_find")
    {
        SubCommand = "close";
    }
}
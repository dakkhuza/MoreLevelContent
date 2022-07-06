using Barotrauma;
using Barotrauma.Networking;

namespace MoreLevelContent.Networking
{
    /// <summary>
    /// Client
    /// </summary>
    public static partial class NetUtil
    {
        public static void SendServer(IWriteMessage outMsg, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable) => GameMain.LuaCs.Networking.Send(outMsg, deliveryMethod);
    }
}

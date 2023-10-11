using Barotrauma;
using Barotrauma.Networking;

namespace MoreLevelContent.Networking
{
    /// <summary>
    /// Client
    /// </summary>
    public static partial class NetUtil
    {
        internal static void SendServer(IWriteMessage outMsg, DeliveryMethod deliveryMethod = DeliveryMethod.Reliable)
        {
            if (GameMain.IsSingleplayer) return;
            GameMain.LuaCs.Networking.Send(outMsg, deliveryMethod);
        }
            
    }
}

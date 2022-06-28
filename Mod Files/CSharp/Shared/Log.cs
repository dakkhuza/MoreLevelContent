using Barotrauma;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;

namespace MoreLevelContent.Shared
{
    public static class Log
    {
        private static readonly bool debug = false;
        private static readonly bool verbose = false;

        public static void Debug(string msg) => LogBase($"ILO DEBUG: {msg}");
        public static void Error(string msg) => LogBase($"ILO ERROR: {msg}");
        public static void InternalDebug(string msg) => LogBase($"ILO INTERNAL DEBUG: {msg}");

        public static void Verbose(string msg) 
        { 
            if (!verbose) return; 
            LogBase($"ILO VERBOSE: {msg}"); 
        }

        private static void LogBase(string msg)
        {
            if (!debug) return;

            DebugConsole.ThrowError(msg);

#if SERVER
            if (GameMain.Server != null)
            {
                msg = "SERVER - " + msg;
                foreach (var c in GameMain.Server.ConnectedClients)
                {
                    GameMain.Server.SendDirectChatMessage(ChatMessage.Create("SERVER - ", msg, ChatMessageType.Console, null, textColor: Color.Red), c);
                }

                GameServer.Log(msg, ServerLog.MessageType.Error);
            }
#endif

        }
    }
}

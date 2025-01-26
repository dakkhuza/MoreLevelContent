using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;

namespace MoreLevelContent.Shared
{
    public static class Log
    {
        private static readonly bool verbose = true;

        public static void Debug(string msg) => LogBase("MLC D ", msg, "null", Color.MediumPurple);
        public static void Warn(string msg) => LogBase("MLC W ", msg, "null", Color.Yellow);
        public static void Error(string msg) => LogBase("MLC E ", msg, "null", Color.Red);
        public static void InternalDebug(string msg)
        {
            if (!ConfigManager.Instance.Config.Client.Internal) return;
            LogBase("MLC ID", msg, "null", Color.Purple);
        }

        public static void Verbose(string msg)
        {
            if (!ConfigManager.Instance.Config.Client.Verbose) return;
            LogBase("MLC V", msg, "null", Color.LightGray);
        }

        private static void LogBase(string prefix, string message, string empty, Color col)
        {
			if (message == null) { message = empty; }
			string str = message.ToString();

 			for (int i = 0; i < str.Length; i += 1024)
 			{
 				string subStr = str.Substring(i, Math.Min(1024, str.Length - i));
 
#if SERVER
 				if (GameMain.Server != null)
 				{
 					foreach (var c in GameMain.Server.ConnectedClients)
 					{
 						GameMain.Server.SendDirectChatMessage(ChatMessage.Create("", "[SERVER] " + subStr, ChatMessageType.Console, null, textColor: Color.MediumPurple), c);
 					}
 
 					GameServer.Log("[SERVER] " + prefix + subStr, ServerLog.MessageType.ServerMessage);
 				}
 #endif
 			}

#if SERVER
			DebugConsole.NewMessage("[SERVER] " + message.ToString(), Color.White);
#else
			DebugConsole.NewMessage("[CLIENT] " + message.ToString(), col);
#endif
		}
	}
}

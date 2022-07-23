using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared;
using Barotrauma.MoreLevelContent.Shared.Config;
using MoreLevelContent;
using Barotrauma.Networking;
using MoreLevelContent.Networking;
using System.Collections.Generic;
using System.Linq;
using System;
// ISSUE WITH LEVEL GEN, IT'S THE RUIN MOVE
namespace Barotrauma.MoreLevelContent.Config
{
    /// <summary>
    /// Server
    /// </summary>
    partial class ConfigManager : Singleton<ConfigManager>
    {
        private void SetupServer()
        {
            NetUtil.Register(NetEvent.CONFIG_WRITE_SERVER, ServerRead);
            NetUtil.Register(NetEvent.CONFIG_REQUEST, ConfigRequest);
            // Always init the server with a default config, the first client to join with admin perms will set the config
            // This was due to some issue with reading the config file on the server iirc
            // Maybe revist this in the future?
            DefaultConfig();
        }

        private readonly List<int> correctInstalls = new List<int>();

        #region Networking
        private void ServerRead(object[] args)
        {
            try
            {
                IReadMessage inMsg = (IReadMessage)args[0];
                Client c = (Client)args[1];
                if (!c.HasPermission(ClientPermissions.ManageSettings))
                {
                    Log.Error("No Perms!");
                    return;
                }
                if (!CheckClientVersion(c, inMsg.ReadString()))
                {
                    Log.Debug($"Ignored config from {c.Name} due to them using the wrong version!");
                    return;
                }
                ReadNetConfig(ref inMsg);
                ServerWrite();
            } catch(Exception err)
            {
                Log.Debug(err.ToString());
            }
        }

        private void ServerWrite()
        {
            Log.Debug("Propagating config to all clients...");
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_WRITE_CLIENT);
            WriteConfig(ref outMsg);
            NetUtil.SendAll(outMsg);
        }

        private void ConfigRequest(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            Client c = (Client)args[1];
            string version = inMsg.ReadString();
            if (!CheckClientVersion(c, version)) return; // Exit if the client doesn't have the right version
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_WRITE_CLIENT);
            WriteConfig(ref outMsg);
            NetUtil.SendClient(outMsg, c.Connection);
            Log.Debug($"Sent config to client {c.Name}");
        }

        #endregion

        private bool CheckClientVersion(Client client, string clientVersion)
        {
            if (correctInstalls.Contains(client.ID)) return true;
            if (clientVersion != Main.Version)
            {
                GameMain.Server.SendDirectChatMessage(
                    TextManager.GetServerMessage($"mlc.server.wrongversionclient~[clientversion]={clientVersion}~[serverversion]={Main.Version}").Value,
                    client,
                    ChatMessageType.ServerMessageBox);
                GameMain.Server.SendChatMessage(TextManager.GetServerMessage($"mlc.server.wrongversion~[client]={client.Name}~[clientversion]={clientVersion}~[serverversion]={Main.Version}").Value);
                return false;
            }

            GameMain.Server.SendChatMessage(TextManager.GetServerMessage($"mlc.server.installed~[client]={client.Name}").Value);
            correctInstalls.Add(client.ID);
            return true;
        }

    }
}

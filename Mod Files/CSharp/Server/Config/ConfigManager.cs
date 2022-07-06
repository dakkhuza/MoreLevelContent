using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared;
using Barotrauma.MoreLevelContent.Shared.Config;
using MoreLevelContent;
using Barotrauma.Networking;
using MoreLevelContent.Networking;
using System.Collections.Generic;
using System.Linq;

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
            NetUtil.Register(NetEvent.PONG_SERVER, ClientPong);
            Main.Hook("clientConnected", "MLC::AddClientToWaitList", AddClientToPending);
            Main.Hook("writeClientList", "MLC::UpdateWaitList", UpdatePending);
        }

        private readonly List<byte> pendingClients = new List<byte>();
        private readonly List<byte> waitClients = new List<byte>();
        private const int waitTime = 10;

        #region Networking
        private void ServerRead(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            Client c = (Client)args[1];
            if (!c.HasPermission(ClientPermissions.ManageSettings))
            {
                Log.Error("No Perms!");
                return;
            }
            Log.Debug($"Got config from {c.Name}");
            ReadNetConfig(ref inMsg);
            ServerWrite();
        }

        private void ServerWrite()
        {
            Log.Debug("Propagating config to all clients...");
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_WRITE_CLIENT);
            Config.WriteTo(ref outMsg);
            NetUtil.SendAll(outMsg);
        }

        private void ConfigRequest(object[] args)
        {
            Client c = (Client)args[1];
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_WRITE_CLIENT);
            Config.WriteTo(ref outMsg);
            NetUtil.SendClient(outMsg, c.Connection);
            Log.Debug($"Sent config to client {c.Name}");
        }

        #endregion

        #region Client Checking

        private object AddClientToPending(object[] args)
        {
            Client client = (Client)args[0];
            pendingClients.Add(client.ID);
            Log.Debug($"Added client {client.Name} to pending");
            return null;
        }

        private object UpdatePending(object[] args)
        {
            Client client = (Client)args[0];
            Log.Debug("Updating pending list");
            if (!GameMain.Server.FileSender.ActiveTransfers.Any(t => t.Connection == client.Connection) && pendingClients.Contains(client.ID))
            {
                // Add to wait list if client is no longer downloading
                waitClients.Add(client.ID);
                _ = pendingClients.Remove(client.ID);
                _ = SendPing(client);
            }
            return null;
        }

        private object SendPing(Client client)
        {
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.PING_CLIENT);
            outMsg.Write(Main.Version);
            StartPongWait(client);
            NetUtil.SendClient(outMsg, client.Connection);
            Log.Debug($"Sent ping to client {client.Name}");
            return null;
        }

        /// <summary>
        /// Start the wait timer for a client pong
        /// </summary>
        /// <param name="newClient"></param>
        private void StartPongWait(Client newClient)
        {
            pendingClients.Add(newClient.ID);
            GameMain.LuaCs.Timer.Wait((_) => EndPongWait(newClient), waitTime * 1000);
            Log.Debug($"Started wait for client {newClient.Name}");
        }

        /// <summary>
        /// Remove the client from the pong list
        /// </summary>
        /// <param name="args"></param>
        private void ClientPong(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            Client client = (Client)args[1];
            _ = pendingClients.Remove(client.ID);
            Log.Debug($"Got pong from {client.Name}");
            CheckClientVersion(client, inMsg.ReadString());
        }

        private void CheckClientVersion(Client client, string clientVersion)
        {
            if (clientVersion != Main.Version)
            {
                GameMain.Server.SendDirectChatMessage(
                    TextManager.GetServerMessage($"mlc.server.wrongversionclient~[clientversion]={clientVersion}~[serverversion]={Main.Version}").Value,
                    client,
                    ChatMessageType.ServerMessageBox);
                GameMain.Server.SendChatMessage(TextManager.GetServerMessage($"mlc.server.wrongversion~[client]={client.Name}").Value, ChatMessageType.Server);
            }
        }

        /// <summary>
        /// End the pong wait for the client
        /// </summary>
        private void EndPongWait(Client client)
        {
            if (pendingClients.Contains(client.ID))
            {
                _ = pendingClients.Remove(client.ID);
                Log.Debug($"Pong wait ended for client {client.Name}");
                GameMain.Server.SendDirectChatMessage(
                    $"Your client did not respond to the servers ping (>{waitTime}s wait)\nPlease install More Level Content, Client Side Lua and CS for Barotrauma",
                    client,
                    ChatMessageType.ServerMessageBox);
                GameMain.Server.SendChatMessage($"Client {client.Name} did not respond (>{waitTime}s) to the servers ping request", ChatMessageType.Server);
            }
        }

        #endregion
    }
}

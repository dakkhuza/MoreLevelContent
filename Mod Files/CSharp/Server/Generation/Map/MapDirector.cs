using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.Networking;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation.Pirate;
using System;
using System.Linq;

namespace MoreLevelContent.Shared.Generation
{
    // Server
    public partial class MapDirector : Singleton<MapDirector>
    {
        partial void SetupProjSpecific()
        {
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_REQUEST, RequestConnectionEquality);
            NetUtil.Register(NetEvent.MAP_REQUEST_STATE, RespondToMapStateRequest);
        }

        private void RespondToMapStateRequest(object[] args)
        {
            Client client = (Client)args[1];
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.MAP_SEND_STATE);

            WriteMsg();

            NetUtil.SendClient(outMsg, client.Connection);
            Log.Debug($"Sent map state request to {client.Name}");


            void WriteMsg()
            {
                if (GameMain.GameSession.Campaign is not MultiPlayerCampaign campaign)
                {
                    outMsg.WriteByte((byte)MapSyncState.NotCampaign);
                    return;
                }
                if (campaign.Map == null)
                {
                    outMsg.WriteByte((byte)MapSyncState.MapNotCreated);
                    return;
                }
                outMsg.WriteByte((byte)MapSyncState.MapSynced);
                var activeDistressBeacons = campaign.Map.Connections.Where(c => c.LevelData.MLC().HasDistress);
                var count = activeDistressBeacons.Count();
                if (count > byte.MaxValue)
                {
                    DebugConsole.ThrowError("More Level Content detected more than 255 active distress beacons when trying to respond to a client map state request, what did you do??? This won't work, please reduce the numer!");
                    return;
                }
                outMsg.WriteByte((byte)count);
                foreach (var connection in activeDistressBeacons)
                {
                    int id = ConnectionIdLookup[connection];
                    outMsg.WriteInt16((short)id);
                    outMsg.WriteByte((byte)connection.LevelData.MLC().DistressStepsLeft);
                }
            }
        }

        internal void NotifyMapFeatureRevealed(LocationConnection con, MapFeatureData feature)
        {
            foreach (Client client in GameMain.Server.ConnectedClients)
            {
                NotifyMapFeatureRevealed(client, con, feature);
            }
        }

        internal partial void RoundEnd(CampaignMode.TransitionType transitionType) { }

        private void NotifyMapFeatureRevealed(Client client, LocationConnection con, MapFeatureData feature)
        {
            Int32 conId = MapDirector.ConnectionIdLookup[con];
            var msg = NetUtil.CreateNetMsg(NetEvent.EVENT_REVEALMAPFEATURE);
            msg.WriteIdentifier(feature.Name);
            msg.WriteInt32(conId);
            NetUtil.SendClient(msg, client.Connection);
        }
    }
}

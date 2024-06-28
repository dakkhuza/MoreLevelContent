using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.Networking;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Shared.Generation
{
    public partial class MapDirector : Singleton<MapDirector>
    {
        partial void SetupProjSpecific()
        {
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_REQUEST, RequestConnectionEquality);
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

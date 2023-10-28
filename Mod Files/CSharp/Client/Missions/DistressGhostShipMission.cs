using Barotrauma.Networking;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    partial class DistressGhostshipMission : DistressMission
    {
        public override bool DisplayAsFailed => false;
        public override bool DisplayAsCompleted => State >= 2;
        public override void ClientReadInitial(IReadMessage msg)
        {
            base.ClientReadInitial(msg);
            Log.Debug("message read init");
            missionNPCs.Read(msg);
        }
    }
}

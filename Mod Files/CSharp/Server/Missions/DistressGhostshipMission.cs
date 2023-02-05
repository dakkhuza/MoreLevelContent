using Barotrauma.Networking;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    partial class DistressGhostshipMission
    {
        public override void ServerWriteInitial(IWriteMessage msg, Client c)
        {
            base.ServerWriteInitial(msg, c);
            missionNPCs.Write(msg);
        }
    }
}

using Barotrauma;
using Barotrauma.Networking;
using MoreLevelContent.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    // Server
    partial class DistressEscortMission : DistressMission
    {
        public override void ServerWriteInitial(IWriteMessage msg, Client c)
        {
            base.ServerWriteInitial(msg, c);
            missionNPCs.Write(msg);
        }
    }
}

using Barotrauma;
using Barotrauma.Networking;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    // Client
    partial class DistressEscortMission : DistressMission
    {
        public override bool DisplayAsFailed => State == 1;

        public override void ClientReadInitial(IReadMessage msg)
        {
            base.ClientReadInitial(msg);
            missionNPCs.Read(msg);
            InitCharacters();
        }
    }
}

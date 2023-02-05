using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    // Server
    partial class DistressSubmarineMission : DistressMission
    {
        public override void ServerWriteInitial(IWriteMessage msg, Client c)
        {
            base.ServerWriteInitial(msg, c);
            missionNPCs.Write(msg);
            foreach (var character in rewardLookup.Keys)
            {
                msg.WriteUInt16((ushort)rewardLookup[character]);
            }
        }
    }
}

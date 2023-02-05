using Barotrauma;
using Barotrauma.Networking;
using HarmonyLib;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    // Client
    partial class DistressSubmarineMission : DistressMission 
    {
        public override bool DisplayAsFailed => false;

        public override RichString GetMissionRewardText(Submarine sub) => State == 0 ? base.GetMissionRewardText(sub) : GetBaseMissionRewardText(sub);

        public override void ClientReadInitial(IReadMessage msg)
        {
            base.ClientReadInitial(msg);
            missionNPCs.Read(msg);

            foreach (var character in missionNPCs.characters)
            {
                int reward = msg.ReadUInt16();
                rewardLookup.Add(character, reward);
                character.Info.Title = TextManager.GetWithVariable("currencyformat", "[credits]", string.Format(CultureInfo.InvariantCulture, "{0:N0}", reward));
            }
        }
    }
}

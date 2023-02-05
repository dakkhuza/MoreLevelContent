using Barotrauma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    // Client
    abstract partial class DistressMission : Mission
    {
        public override bool DisplayAsCompleted => false;

        // Hide reward until the end of the round
        public override RichString GetMissionRewardText(Submarine sub) => RichString.Rich(TextManager.GetWithVariable("missionreward", "[reward]", $"‖color:gui.orange‖{(GameMain.GameSession.RoundEnding || DisplayReward ? Reward : "???")}‖end‖"));
        protected RichString GetBaseMissionRewardText(Submarine sub) => base.GetMissionRewardText(sub);
    }
}

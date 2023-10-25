using Barotrauma;
using MoreLevelContent.Shared.Data;
using System;
using System.Linq;
using System.Reflection;

namespace MoreLevelContent.Missions
{
    // Shared
    abstract partial class DistressMission : Mission
    {
        protected bool DisplayReward;
        private readonly MethodInfo _triggerEventMethod;
        public DistressMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub) => DisplayReward = prefab.ConfigElement.GetAttributeBool("displayreward", false);

        protected override void EndMissionSpecific(bool completed)
        {
            failed = !completed;
            if (completed || Submarine.MainSub.AtEndExit || Submarine.MainSub.AtStartExit)
            {
                if (level?.LevelData != null && Prefab.Tags.Contains("distress"))
                {
                    level.LevelData.MLC().HasDistress = false;
                }
            }
        }
    }
}

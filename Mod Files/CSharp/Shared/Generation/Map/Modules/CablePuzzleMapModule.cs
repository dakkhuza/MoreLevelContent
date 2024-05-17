using Barotrauma;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class CablePuzzleMapModule : MapModule
    {
        protected override void InitProjSpecific() { }

        public override void OnAddExtraMissions(CampaignMode __instance, LevelData levelData)
        {
            var missions = MissionPrefab.Prefabs.Where(m => m.Tags.Contains("cablepuzzle")).OrderBy(m => m.UintIdentifier);
            if (!missions.Any())
            {
                Log.Error("Failed to find any cable puzzle missions!");
                return;
            }

            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            var cablePuzzles = ToolBox.SelectWeightedRandom(missions, p => p.Commonness, rand);
            if (!__instance.Missions.Any(m => m.Prefab.Type == cablePuzzles.Type))
            {
                List<Mission> _extraMissions = (List<Mission>)Instance.extraMissions.GetValue(__instance);
                Mission inst = cablePuzzles.Instantiate(__instance.Map.SelectedConnection.Locations, Submarine.MainSub);
                _extraMissions.Add(inst);
                Instance.extraMissions.SetValue(__instance, _extraMissions);
                Log.Debug("Added cable puzzle mission to extra missions!");
            }
        }
    }
}

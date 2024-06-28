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
            if (levelData.Type == LevelData.LevelType.Outpost) return; // Ignore outpost levels
            LevelData_MLCData data = levelData.MLC();
            if (data.RelayStationStatus == RelayStationStatus.None) return; // Do nothing if we don't have a relay station

            var missions = MissionPrefab.Prefabs.Where(m => m.Tags.Contains("relayrepair")).OrderBy(m => m.UintIdentifier);
            if (!missions.Any())
            {
                Log.Error("Failed to find any cable puzzle missions!");
                return;
            }

            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            var cablePuzzleMissionPrefab = ToolBox.SelectWeightedRandom(missions, p => p.Commonness, rand);

            // Add the mission if the station is inactive
            if (!__instance.Missions.Any(m => m.Prefab.Type == cablePuzzleMissionPrefab.Type) && data.RelayStationStatus == RelayStationStatus.Inactive)
            {
                List<Mission> _extraMissions = (List<Mission>)Instance.extraMissions.GetValue(__instance);
                Mission inst = cablePuzzleMissionPrefab.Instantiate(__instance.Map.SelectedConnection.Locations, Submarine.MainSub);
                _extraMissions.Add(inst);
                Instance.extraMissions.SetValue(__instance, _extraMissions);
                Log.Debug("Added relay staion mission to extra missions!");
                return;
            }

            // Otherwise the station is active, so we need to assign the sub file
            var configElement = cablePuzzleMissionPrefab.ConfigElement.GetChildElement("Submarine");
            CablePuzzleMission.SetSub(configElement, cablePuzzleMissionPrefab);
            Log.Debug("Set the relay station sub for a completed relay station");
        }

        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection)
        {
            LevelData_MLCData levelData = __instance.MLC();
            if (levelData.HasBeaconConstruction) return; // Ignore levels with a construction site
            RollForRelay(__instance, levelData, locationConnection);
        }

        // Map Migration
        public override void OnMapLoad(Map __instance)
        {
            if (!__instance.Connections.Any(c => c.LevelData.MLC().HasRelayStation))
            {
                Log.Debug("Map has no relay stations, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];

                    // See if we should generate a construction site
                    LevelData_MLCData extraData = connection.LevelData.MLC();
                    RollForRelay(connection.LevelData, extraData, connection);
                }
            }
            else
            {
                Log.Debug("Map has relay stations");
            }
        }

        private void RollForRelay(LevelData levelData, LevelData_MLCData extraData, LocationConnection locationConnection)
        {
            var rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            if (!levelData.HasBeaconStation && !levelData.MLC().HasBeaconConstruction)
            {
                double roll = rand.NextDouble();
                // Relay stations have a 15% chance to spawn on any connection
                extraData.RelayStationStatus = roll < 0.15f ? RelayStationStatus.Inactive : RelayStationStatus.None;
            }
        }

    }
}

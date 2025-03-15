using Barotrauma;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using Barotrauma.MoreLevelContent.Config;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class ConstructionMapModule : MapModule
    {
        public override void OnAddExtraMissions(CampaignMode __instance, LevelData levelData)
        {
            if (levelData.Type == LevelData.LevelType.Outpost) return; // Ignore outpost levels
            LevelData_MLCData data = levelData.MLC();
            if (data.HasBeaconConstruction && ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.EnableConstructionSites)
            {
                var constructionMissions = MissionPrefab.Prefabs.Where(m => m.Tags.Contains("beaconconstruction")).OrderBy(m => m.UintIdentifier);
                if (constructionMissions.Any())
                {
                    Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
                    var beaconMissionPrefab = ToolBox.SelectWeightedRandom(constructionMissions, p => (float)p.Commonness, rand);
                    if (!__instance.Missions.Any(m => m.Prefab.Type == beaconMissionPrefab.Type))
                    {
                        List<Mission> _extraMissions = (List<Mission>)Instance.extraMissions.GetValue(__instance);
                        Mission inst = beaconMissionPrefab.Instantiate(__instance.Map.SelectedConnection.Locations, Submarine.MainSub);
                        _extraMissions.Add(inst);
                        Instance.extraMissions.SetValue(__instance, _extraMissions);
                        Log.Debug("Added beacon construction mission to extra missions!");
                    }
                }
                else
                {
                    Log.Error("Failed to find any beacon construction missions!");
                }
            }
        }
        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection)
        {
            LevelData_MLCData levelData = __instance.MLC();
            if (levelData.HasRelayStation) return;
            TrySpawnBeaconConstruction(__instance, levelData, locationConnection);
        }
        public override void OnMapLoad(Map __instance)
        {
            if (!__instance.Connections.Any(c => c.LevelData.MLC().HasBeaconConstruction))
            {
                Log.Debug("Map has no construction sites, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];

                    // See if we should generate a construction site
                    LevelData_MLCData extraData = connection.LevelData.MLC();
                    TrySpawnBeaconConstruction(connection.LevelData, extraData, connection);
                }
            }
            else
            {
                Log.Debug("Map has construction sites");
            }
        }

        private void TrySpawnBeaconConstruction(LevelData levelData, LevelData_MLCData extraData, LocationConnection locationConnection)
        {
            var rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            // Place some beacon stations
            if (!levelData.IsBeaconActive)
            {
                double roll = rand.NextDouble();
                double chance = locationConnection.Locations.Select(l => l.Type.BeaconStationChance).Min();
                extraData.HasBeaconConstruction = roll < (chance / 1.2); // construction sites have half the chance to spawn as regular beacon stations
                if (extraData.HasBeaconConstruction)
                {
                    CreateBeaconConstruction(levelData, rand, extraData);
                    levelData.HasBeaconStation = false;
                }
            }
        }
        private void CreateBeaconConstruction(LevelData __instance, MTRandom rand, LevelData_MLCData levelData)
        {
            List<SupplyType> possibleSupplies = new();
            AddSupply(SupplyType.Electric, 4);
            AddSupply(SupplyType.Structure, 4);
            AddSupply(SupplyType.Utility, 4);

            int diffClamped = (int)(__instance.Difficulty / 10);
            // Always request at least one
            int totalRequested = 1 + rand.Next(diffClamped + 1);

            for (int i = 0; i < totalRequested; i++)
            {
                int index = rand.Next(possibleSupplies.Count);
                SupplyType requestedSupply = possibleSupplies[index];
                possibleSupplies.RemoveAt(index);
                switch (requestedSupply)
                {
                    case SupplyType.Electric:
                        levelData.RequestedE++;
                        break;
                    case SupplyType.Structure:
                        levelData.RequestedS++;
                        break;
                    case SupplyType.Utility:
                        levelData.RequestedU++;
                        break;
                }
            }

            Log.Debug("Created a beacon construction mission");

            void AddSupply(SupplyType type, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    possibleSupplies.Add(type);
                }
            }

            if (levelData.HasBeaconConstruction) __instance.HasBeaconStation = false;
        }

        protected override void InitProjSpecific() { }

        enum SupplyType
        {
            Electric,
            Structure,
            Utility
        }

    }
}

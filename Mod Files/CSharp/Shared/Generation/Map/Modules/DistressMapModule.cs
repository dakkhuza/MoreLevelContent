using Barotrauma.Networking;
using Barotrauma;
using System.Collections.Generic;
using System;
using System.Linq;
using MoreLevelContent.Shared.Data;
using Barotrauma.MoreLevelContent.Config;
using MoreLevelContent.Networking;

namespace MoreLevelContent.Shared.Generation
{
    // Shared
    internal partial class DistressMapModule : MapModule
    {
        private readonly List<Mission> _internalMissionStore = new();
        private static DistressMapModule _instance;

        public DistressMapModule()
        {
            _instance = this;
            InitProjSpecific();
        }

        internal void UpdateDistressBeacons(Map __instance)
        {
            foreach (LocationConnection connection in __instance.Connections.Where(c => c.LevelData.MLC().HasDistress))
            {
                // TODO: When multiple world steps happen at once in a long mission
                // this cause a distress to skip from active -> faint -> off before
                // the player has seen any notification of it. There could be a way
                // of avoiding this by counting the world steps before doing this
                // instead of doing it every world step
                var levelData = connection.LevelData.MLC();
                levelData.DistressStepsLeft--;
                if (levelData.DistressStepsLeft <= 0)
                {
                    levelData.HasDistress = false;
                    SendDistressUpdate("mlc.distress.lost", connection);
                }
                if (levelData.DistressStepsLeft == 3) SendDistressUpdate("mlc.distress.faint", connection);
            }
        }
        internal void Shared_CreateDistress(Map __instance, Random random, int iteration = 0)
        {
            // Spawn new distress
            LocationConnection connection = WalkConnection(__instance.StartLocation, random, 4);
            if (connection.LevelData.MLC().HasDistress)
            {
                if (iteration > 5)
                {
                    Log.Debug("Could not find a suitable spawn for a distress beacon after 5 attempts!");
                    return;
                }
                Shared_CreateDistress(__instance, random, iteration++);
                return;
            }
            Log.Debug($"Picked location at: {connection.CenterPos} for distress");
            connection.LevelData.MLC().HasDistress = true;
            connection.LevelData.MLC().DistressStepsLeft = random.Next(4, 8);
            SendDistressUpdate("mlc.distress.new", connection);
        }
        private void SendDistressUpdate(string updateType, LocationConnection connection)
        {
#if CLIENT
            string msg = TextManager.GetWithVariables(updateType, ("[location1]", $"‖color:gui.orange‖{connection.Locations[0].Name}‖end‖"), ("[location2]", $"‖color:gui.orange‖{connection.Locations[1].Name}‖end‖")).Value;
            SendChatUpdate(msg);
#endif
        }

        public override void OnRoundStart(LevelData levelData)
        {
            _internalMissionStore.Clear();

            if (levelData == null) return;
            if (!Main.IsCampaign) return;
            if (!levelData.MLC().HasDistress) return;

            // Filter distress prefabs
            var distressMissions = MissionPrefab.Prefabs.Where(m => m.Identifier == "distress_ghostship_bandit" &&
            m.Tags.Any(t => t.Equals("distress", StringComparison.OrdinalIgnoreCase))).OrderBy(m => m.UintIdentifier);

            if (distressMissions.Any())
            {
                try
                {
                    Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
                    var distressMissionPrefab = ToolBox.SelectWeightedRandom(distressMissions, p => p.Commonness, rand);
                    Mission inst = distressMissionPrefab.Instantiate(GameMain.GameSession.Map.SelectedConnection.Locations, Submarine.MainSub);
                    AddExtraMission(inst);
                    _internalMissionStore.Add(inst);
                    Log.Debug("Added distress mission to extra missions!");
                } catch(Exception e) { Log.Error(e.ToString()); }
            }
            else
            {
                Log.Error("Failed to find any distress missions!");
            }
        }

        public override void OnAddExtraMissions(CampaignMode __instance, LevelData levelData)
        {
            if (!_internalMissionStore.Any()) return;
            foreach (Mission mission in _internalMissionStore)
            {
                AddExtraMission(mission);
            }
            _internalMissionStore.Clear();
        }

        private void AddExtraMission(Mission mission)
        {
            List<Mission> _extraMissions = (List<Mission>)Instance.extraMissions.GetValue(GameMain.GameSession.GameMode);
            _extraMissions.Add(mission);
            Instance.extraMissions.SetValue(GameMain.GameSession.GameMode, _extraMissions);
        }

        public override void OnProgressWorld(Map __instance)
        {
            UpdateDistressBeacons(__instance);

            if (!Main.IsClient)
            {
                TrySpawnDistress(GameMain.GameSession.Map);
            }
        }

        private void TrySpawnDistress(Map __instance, bool force = false)
        {
            // Check if we're at the max
            int activeDistressCalls = __instance.Connections.Where(c => c.LevelData.MLC().HasDistress).Count();
            if (activeDistressCalls > ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons)
            {
                Log.Debug($"Skipped creating new distress due to being at the limit ({ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons})");
                return;
            }

            // If we're not, lets roll to see if we should make a new distress signal
            float chance = Rand.Value(Rand.RandSync.Unsynced);
            Log.InternalDebug($"{chance} >= {ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.DistressSpawnPercentage} ({chance >= ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.DistressSpawnPercentage})");
            if (chance >= ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.DistressSpawnPercentage && !force) return;

            // Since we're creating a new distress signal, lets get a seed to send to the clients
            int seed = Rand.GetRNG(Rand.RandSync.Unsynced).Next();
            Random rand = new MTRandom(seed);
            Shared_CreateDistress(__instance, rand);

#if SERVER
            // inform clients of the new distress beacon
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.MAP_SEND_NEWDISTRESS);
            msg.WriteInt32(seed);
            NetUtil.SendAll(msg);
#endif
        }

        internal static void ForceDistress()
        {
            Log.Debug("Force creating distress beacon");
            _instance.TrySpawnDistress(GameMain.GameSession.Map, true);
        }

        public override void OnMapGenerate(Map __instance)
        {
            SpawnStartingDistressBeacon();


            void SpawnStartingDistressBeacon()
            {
                List<LocationConnection> startingConnections = __instance.Connections.Where(c => c.Locations.Any(L => L.GetZoneIndex(__instance) == 1)).ToList();
                var levelData = startingConnections[Rand.Range(0, startingConnections.Count())].LevelData.MLC();
                levelData.HasDistress = true;
                levelData.DistressStepsLeft = Rand.Range(4, 8);
                Log.Debug("Spawned starting distress beacon");
            }
        }
    }
}

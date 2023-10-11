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
        private static bool _spawnStartingBeacon = false;
        private int worldProgressCount = 0;
        const int MAX_DISTRESS_CREATE_ATTEMPTS = 5;

        public DistressMapModule()
        {
            _instance = this;
            InitProjSpecific();

        }


        public override void OnProgressWorld_Step(Map __instance)
        {
            if (Main.IsClient) return;
            Log.Debug("Step World");
            worldProgressCount++;
        }
        public override void OnProgressWorld(Map __instance)
        {
            if (Main.IsClient) return;
            UpdateDistressBeacons(__instance, worldProgressCount);
            worldProgressCount = 0;
        }

        public override void OnRoundStart(LevelData levelData)
        {
            _internalMissionStore.Clear();

            if (levelData == null) return;
            if (!Main.IsCampaign) return;

            TrySpawnDistress(GameMain.GameSession.Map, _spawnStartingBeacon);
            _spawnStartingBeacon = false;

            if (!levelData.MLC().HasDistress)
            {
                Log.Debug("Level has no distress mission");
                return;
            }

            // Filter distress prefabs
            var distressMissions = MissionPrefab.Prefabs.Where(m => //m.Identifier == "distress_ghostship_alienship" &&
            m.Tags.Contains("distress")).OrderBy(m => m.UintIdentifier);

            if (distressMissions.Any())
            {
                try
                {
                    Log.Debug("Adding distress mission");
                    Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
                    var distressMissionPrefab = ToolBox.SelectWeightedRandom(distressMissions, p => p.Commonness, rand);
                    Mission inst = distressMissionPrefab.Instantiate(GameMain.GameSession.Map.SelectedConnection.Locations, Submarine.MainSub);
                    AddExtraMission(inst);
                    _internalMissionStore.Add(inst);
                    Log.Debug("Added distress mission to extra missions!");
                }
                catch (Exception e) { Log.Error(e.ToString()); }
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
        public override void OnMapGenerate(Map __instance) => _spawnStartingBeacon = false; // we're just going to remove the starting distress beacon because it causes issues

        internal void UpdateDistressBeacons(Map __instance, int stepCount)
        {
            // Get connections that have distress signals
            foreach (LocationConnection connection in __instance.Connections.Where(c => c.LevelData.MLC().HasDistress))
            {
                // skip locations that are close
                if (GameMain.GameSession.Campaign.Map.CurrentLocation.Connections.Contains(connection))
                {
                    Log.Debug("Skipped updating distress beacon for being too close");
                    continue;
                }


                // TODO: When multiple world steps happen at once in a long mission
                // this cause a distress to skip from active -> faint -> off before
                // the player has seen any notification of it. There could be a way
                // of avoiding this by counting the world steps before doing this
                // instead of doing it every world step
                var levelData = connection.LevelData.MLC();
                int distressStepsLeft = levelData.DistressStepsLeft;
                int distressStepsLeftAfterSubtraction = levelData.DistressStepsLeft - stepCount;
                int stepsToSubtract = stepCount;
                bool sendFaint = false;

                // Prevent distress beacons from going from active -> inactive in a single transition
                if (distressStepsLeftAfterSubtraction <= 3)
                {
                    stepsToSubtract = 1;
                    if (distressStepsLeft > 3)
                    {
                        levelData.DistressStepsLeft = 4;
                        Log.Debug("Set distress beacon to 4");
                    }
                }

                if (distressStepsLeft > 3 && distressStepsLeftAfterSubtraction < 3)
                {
                    SendDistressUpdate("mlc.distress.faint", connection);
                    sendFaint = true;
                }

                levelData.DistressStepsLeft -= stepsToSubtract;

                if (levelData.DistressStepsLeft == 0)
                {
                    levelData.HasDistress = false;
                    levelData.DistressStepsLeft = 0;
                    SendDistressUpdate("mlc.distress.lost", connection);
                }

                // Send update to client
#if SERVER
                if (GameMain.IsMultiplayer)
                {
                    IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.MAP_UPDATE_DISTRESS);
                    outMsg.WriteUInt32(MapDirector.ConnectionIdLookup[connection]);
                    outMsg.WriteBoolean(levelData.HasDistress);
                    outMsg.WriteBoolean(sendFaint);
                    NetUtil.SendAll(outMsg);
                }
#endif
            }
        }

        /*
        internal void UpdateDistressBeacons(Map __instance)
        {
            // probably causes issues with DE on lower end computers
            // probably cache levels with distress beacons
            foreach (LocationConnection connection in __instance.Connections.Where(c => c.LevelData.MLC().HasDistress))
            {
                // skip locations that are close
                if (GameMain.GameSession.Campaign.Map.CurrentLocation.Connections.Contains(connection)) continue;

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
        */

        private void SendDistressUpdate(string updateType, LocationConnection connection)
        {
#if CLIENT
            string msg = TextManager.GetWithVariables(updateType, ("[location1]", $"‖color:gui.orange‖{connection.Locations[0].Name}‖end‖"), ("[location2]", $"‖color:gui.orange‖{connection.Locations[1].Name}‖end‖")).Value;
            SendChatUpdate(msg);
#endif
        }

        private void AddExtraMission(Mission mission)
        {
            List<Mission> _extraMissions = (List<Mission>)Instance.extraMissions.GetValue(GameMain.GameSession.GameMode);
            _extraMissions.Add(mission);
            Instance.extraMissions.SetValue(GameMain.GameSession.GameMode, _extraMissions);
        }

        private void TrySpawnDistress(Map __instance, bool force = false)
        {
            if (Main.IsClient) return;
            // Check if we're at the max
            int activeDistressCalls = __instance.Connections.Where(c => c.LevelData.MLC().HasDistress).Count();
            if (activeDistressCalls > ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons)
            {
                if (force)
                {
                    Log.Debug("Ignoring max distress cap due to force creation");
                } else
                {
                    Log.Debug($"Skipped creating new distress due to being at the limit ({ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons})");
                    return;
                }
            }

            // If we're not, lets roll to see if we should make a new distress signal
            float chance = Rand.Value(Rand.RandSync.Unsynced);
            Log.InternalDebug($"{chance} >= {ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.DistressSpawnPercentage} ({chance >= ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.DistressSpawnPercentage})");
            if (chance >= ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.DistressSpawnPercentage && !force) return;

            // Lets get a random instance to use
            int seed = Rand.GetRNG(Rand.RandSync.Unsynced).Next();
            Random rand = new MTRandom(seed);

            // Find a location connection to spawn a distress beacon at
            LocationConnection targetConnection = WalkConnection(__instance.CurrentLocation, rand, 4);
            int stepsLeft = rand.Next(4, 8);
            if (!MapDirector.ConnectionIdLookup.ContainsKey(targetConnection)) return; // how does this happen?

            CreateDistress(targetConnection, stepsLeft);

#if SERVER
            if (GameMain.IsMultiplayer)
            {
                // inform clients of the new distress beacon
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.MAP_SEND_NEWDISTRESS);
                msg.WriteUInt32((uint)MapDirector.ConnectionIdLookup[targetConnection]);
                msg.WriteByte((byte)stepsLeft);
                NetUtil.SendAll(msg);
            }
#endif
        }

        private void CreateDistress(LocationConnection connection, int stepsLeft)
        {
            connection.LevelData.MLC().HasDistress = true;
            connection.LevelData.MLC().DistressStepsLeft = stepsLeft;
            SendDistressUpdate("mlc.distress.new", connection);
        }

        private void UpdateDistress(LocationConnection connection, int newStepCount)
        {
            int currentSteps = connection.LevelData.MLC().DistressStepsLeft;
            connection.LevelData.MLC().DistressStepsLeft = newStepCount;
            if (newStepCount == 0) connection.LevelData.MLC().HasDistress = false;
            if (currentSteps > 3 && newStepCount < 3)
            {
                if (newStepCount == 0) SendDistressUpdate("mlc.distress.lost", connection);
                else SendDistressUpdate("mlc.distress.faint", connection);
            }
        }

        internal static void ForceDistress()
        {
            Log.Debug("Force creating distress beacon");
            _instance.TrySpawnDistress(GameMain.GameSession.Map, true);
        }
    }
}

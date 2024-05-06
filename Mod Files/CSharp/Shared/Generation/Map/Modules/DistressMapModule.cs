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
        public static bool ForceSpawnDistress = false;
        public static string ForcedMissionIdentifier = "";

        private readonly List<Mission> _internalMissionStore = new();
        private static DistressMapModule _instance;
        private static bool _spawnStartingBeacon = false;
        const int MAX_DISTRESS_CREATE_ATTEMPTS = 5;
        const int DISTRESS_MIN_DIST = 1;
        const int DISTRESS_MAX_DIST = 3;

        public DistressMapModule()
        {
            _instance = this;
            InitProjSpecific();
        }

        internal void UpdateDistressBeacons(Map __instance)
        {
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
        private void SendDistressUpdate(string updateType, LocationConnection connection)
        {
#if CLIENT
            string msg = TextManager.GetWithVariables(updateType, ("[location1]", $"‖color:gui.orange‖{connection.Locations[0].DisplayName}‖end‖"), ("[location2]", $"‖color:gui.orange‖{connection.Locations[1].DisplayName}‖end‖")).Value;
            SendChatUpdate(msg);
#endif
        }

        public override void OnRoundStart(LevelData levelData)
        {
            _internalMissionStore.Clear();

            if (levelData == null) return;
            if (!Main.IsCampaign) return;

            TrySpawnDistress(GameMain.GameSession.Map, _spawnStartingBeacon);
            _spawnStartingBeacon = false;

            if (!levelData.MLC().HasDistress && !ForceSpawnDistress)
            {
                Log.Debug("Level has no distress mission");
                return;
            }
                
            if (TryGetMissionByTag("distress", levelData, out MissionPrefab prefab, ForcedMissionIdentifier))
            {
                Log.Debug("Adding distress mission");
                Mission inst = prefab.Instantiate(GameMain.GameSession.Map.SelectedConnection.Locations, Submarine.MainSub);
                AddExtraMission(inst); // weird
                _internalMissionStore.Add(inst);
                Log.Debug("Added distress mission to extra missions!");
            } else
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

        public override void OnProgressWorld(Map __instance) => UpdateDistressBeacons(__instance);

        private void TrySpawnDistress(Map __instance, bool force = false)
        {
            if (Main.IsClient) return;

            if (__instance == null || __instance.Connections.Count == 0)
            {
                Log.Debug("Skipped trying to create a distress beacon as there was no map connections");
                return;
            }

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
            int dist = Rand.Range(DISTRESS_MIN_DIST, DISTRESS_MAX_DIST, Rand.RandSync.Unsynced);
            LocationConnection targetConnection = WalkConnection(__instance.CurrentLocation, rand, dist);
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

        internal static void ForceDistress()
        {
            Log.Debug("Force creating distress beacon");
            _instance.TrySpawnDistress(GameMain.GameSession.Map, true);
        }
    }
}

using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using Barotrauma.Networking;
using System.Reflection.Emit;
using System.Diagnostics;

namespace MoreLevelContent.Shared.Generation
{
    // Shared
    public partial class MapDirector : Singleton<MapDirector>
    {
        internal static readonly Dictionary<Int32, LocationConnection> IdConnectionLookup = new();
        internal static readonly Dictionary<LocationConnection, Int32> ConnectionIdLookup = new();
#if CLIENT
        private static bool _validatedConnectionLookup = false;
#endif
        public override void Setup()
        {
            // Map
            var map_ctr_loadFromFile = AccessTools.Constructor(typeof(Map), new Type[] { typeof(CampaignMode), typeof(XElement) });
            var map_ctr_createNewMap = AccessTools.Constructor(typeof(Map), new Type[] { typeof(CampaignMode), typeof(string) });

            var map_save = typeof(Map).GetMethod(nameof(Map.Save));
            var map_progressworld = AccessTools.Method(typeof(Map), "ProgressWorld", new Type[] { typeof(CampaignMode) });

            // Leveldata
            var leveldata_ctr_load = typeof(LevelData).GetConstructor(new Type[] { typeof(XElement), typeof(float?), typeof(bool) });
            var leveldata_ctr_generate = typeof(LevelData).GetConstructor(new Type[] { typeof(LocationConnection) });
            var leveldata_save = typeof(LevelData).GetMethod(nameof(LevelData.Save));

            // GameSession
            var gamesession_StartRound = typeof(GameSession).GetMethod(nameof(GameSession.StartRound), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo) });
            var campaignmode_AddExtraMissions = typeof(CampaignMode).GetMethod(nameof(CampaignMode.AddExtraMissions));

            // level generate

            Check(map_ctr_loadFromFile, "Map Created From File");
            Check(map_ctr_createNewMap, "Map Created From Seed");
            Check(map_save, "map_save");
            Check(map_progressworld, "map_progressworld");
            Check(leveldata_ctr_load, "leveldata_ctr_load");
            Check(leveldata_ctr_generate, "leveldata_ctr_generate");
            Check(leveldata_save, "leveldata_save");
            Check(gamesession_StartRound, "gamesession_startround");
            Check(campaignmode_AddExtraMissions, "campaignmode_addextramissions");

            // Map data
            _ = Main.Harmony.Patch(map_ctr_loadFromFile, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnMapLoad), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(map_ctr_createNewMap, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnMapLoad), BindingFlags.Static | BindingFlags.NonPublic)));

            // Level data
            _ = Main.Harmony.Patch(leveldata_ctr_load, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataLoad), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(leveldata_ctr_generate, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataGenerate), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(leveldata_save, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataSave), BindingFlags.Static | BindingFlags.NonPublic)));

            // Campaign
            _ = Main.Harmony.Patch(campaignmode_AddExtraMissions, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(OnAddExtraMissions))));
            _ = Main.Harmony.Patch(gamesession_StartRound, prefix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(OnPreRoundStart))));
            _ = Main.Harmony.Patch(gamesession_StartRound, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(OnPostRoundStart))));
            extraMissions = AccessTools.Field(typeof(CampaignMode), "extraMissions");

            _ = Main.Harmony.Patch(map_progressworld, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(OnProgressWorld))));

            Modules.Add(new ConstructionMapModule());
            Modules.Add(new DistressMapModule());
            Modules.Add(new PirateOutpostMapModule());
            Modules.Add(new CablePuzzleMapModule());

            Modules.Add(new MapFeatureModule());

            SetupProjSpecific();
        }

        public void ForceDistress()
        {
            var distressModule = (DistressMapModule)Modules.Find(m => m.GetType() == typeof(DistressMapModule));
            distressModule.TrySpawnEvent(GameMain.GameSession.Map, true);
        }

        public void SetForcedDistressMission(bool force, string identifier)
        {
            var distressModule = (DistressMapModule)Modules.Find(m => m.GetType() == typeof(DistressMapModule));
            distressModule.ForceSpawnMission = force;
            distressModule.ForcedMissionIdentifier = identifier;
        }

        partial void SetupProjSpecific();

        public enum MapSyncState
        {
            Syncing,
            NotCampaign,
            MapNotCreated,
            MapSynced
        }

#if CLIENT
        private void ConnectionEqualityCheck(object[] args)
        {
            Log.Debug("Got map connection equality check!");
            IReadMessage inMsg = (IReadMessage)args[0];
            UInt32 connectionCount = inMsg.ReadUInt32();
            if (connectionCount != IdConnectionLookup.Keys.Count)
            {
                KickClient($"The connection lookup generated on your client did not match the one on the server (Client Key Count: {IdConnectionLookup.Keys.Count}, Server Key Count: {connectionCount})"); 
                return;
            }

            for (int i = 0; i < connectionCount - 1; i++)
            {
                Int32 key = inMsg.ReadInt32();
                if (!IdConnectionLookup.ContainsKey(key))
                {
                    KickClient($"The connection lookup generated on your client did not match the one on the server (Client did not contain server key {key})");
                    return;
                }
            }

            Log.Debug("Equality good! Requesting map sync");
            NetUtil.SendServer(NetUtil.CreateNetMsg(NetEvent.MAP_REQUEST_STATE));
        }


        private void KickClient(string reason)
        {
            Log.Error(reason);
            _ = new GUIMessageBox(TextManager.Get("Error"), TextManager.GetWithVariables("MessageReadError", ("[message]", $"MLC ERROR: {reason}")))
            {
                DisplayInLoadingScreens = true
            };
            GameMain.Client.Quit();

        }
#endif

#if SERVER
        private void RequestConnectionEquality(object[] args)
        {
            if (GameMain.GameSession.GameMode.GetType() != typeof(MultiPlayerCampaign)) return;
            Log.Debug("Got request for quality check");
            Client c = (Client)args[1];
            if (IdConnectionLookup.Count == 0)
            {
                c.Kick("Client requested the map equality check before the server generated it. This means the campaign map did not exist on the server when the client requested this request. Are you playing campaign mode?");
                return;
            }
            
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.MAP_CONNECTION_EQUALITYCHECK_SENDCLIENT);
            msg.WriteUInt32((uint)IdConnectionLookup.Keys.Count); // write the total count
            foreach (var key in IdConnectionLookup.Keys)
            {
                msg.WriteUInt32((uint)key);
            }

            NetUtil.SendClient(msg, c.Connection);
        }
#endif

        internal partial void RoundEnd(CampaignMode.TransitionType transitionType);

        private void Check(object info, string name)
        {
            if (info == null) Log.Error(name);
        }

        internal FieldInfo extraMissions;
        internal List<MapModule> Modules = new();

        private static void OnPreRoundStart(GameSession __instance, LevelData levelData)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnPreRoundStart(levelData);
            }
        }

        private static void OnPostRoundStart(GameSession __instance, LevelData levelData)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnPostRoundStart(levelData);
            }
        }


        private static void OnAddExtraMissions(CampaignMode __instance, LevelData levelData)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnAddExtraMissions(__instance, levelData);
            }
        }

        private static void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnLevelDataGenerate(__instance, locationConnection);
            }
        }

        public static void ForceWorldStep() => OnProgressWorld(GameMain.GameSession.Map);

        private static void OnProgressWorld(Map __instance)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnProgressWorld(__instance);
            }
        }

        private static void OnLevelDataLoad(LevelData __instance, XElement element)
        {
            LevelData_MLCData data = new();
            data.LoadData(element);
            __instance.AddData(data);

            foreach (var item in Instance.Modules)
            {
                item.OnLevelDataLoad(__instance, element);
            }
        }

        private static void OnLevelDataSave(LevelData __instance, XElement parentElement)
        {
            XElement levelData = (XElement)parentElement.LastNode;
            LevelData_MLCData data = __instance.MLC();
            data.SaveData(levelData);

            foreach (var item in Instance.Modules)
            {
                item.OnLevelDataSave(__instance, parentElement);
            }
        }

        private static void OnMapLoad(Map __instance)
        {
            Log.Debug("OnMapLoad:Postfix");
            IdConnectionLookup.Clear();
            ConnectionIdLookup.Clear();
            // Generate location connection lookup 
            GenerateConnectionLookup(__instance);

#if CLIENT
            if (!_validatedConnectionLookup && GameMain.IsMultiplayer)
            {
                _validatedConnectionLookup = true;
                NetUtil.SendServer(NetUtil.CreateNetMsg(NetEvent.MAP_CONNECTION_EQUALITYCHECK_REQUEST));
                Log.Debug("Sent request for connection equality");
            } else
            {
                Log.Debug($"Skipped validating the connection lookup: {_validatedConnectionLookup}, {GameMain.IsMultiplayer}");
            }
#endif

            foreach (var item in Instance.Modules)
            {
                item.OnMapLoad(__instance);
            }
        }

        private static void OnMapSave()
        {
            Log.Debug("OnMapSave");
        }

        internal void OnLevelGenerate(LevelData levelData, bool mirror)
        {
            foreach (var item in Modules)
            {
                item.OnLevelGenerate(levelData, mirror);
            }
        }


        private static void GenerateConnectionLookup(Map map)
        {
            for (int i = 0; i < map.Connections.Count; i++)
            {
                var connection = map.Connections[i];
                if (IdConnectionLookup.ContainsKey(i) || ConnectionIdLookup.ContainsKey(connection)) continue; // skip duplicate entries
                IdConnectionLookup.Add(i, connection);
                ConnectionIdLookup.Add(connection, i);
            }
            Log.Debug("Generated map connection lookup");
        }
    }

    internal static class MapExtensions
    {
        internal static int GetZoneIndex(this Location location, Map map)
        {
            float zoneWidth = MapGenerationParams.Instance.Width / MapGenerationParams.Instance.DifficultyZones;
            return MathHelper.Clamp((int)Math.Floor(location.MapPosition.X / zoneWidth) + 1, 1, MapGenerationParams.Instance.DifficultyZones);
        }
    }
}

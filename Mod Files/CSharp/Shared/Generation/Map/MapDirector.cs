using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using System.Linq;
using MoreLevelContent.Shared.Content;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Missions;
using Microsoft.Xna.Framework;
using FarseerPhysics.Collision;
using MoreLevelContent.Networking;
using Barotrauma.Networking;
using MoreLevelContent.Shared.Utils;
using System.Security.Cryptography;

namespace MoreLevelContent.Shared.Generation
{
    public partial class MapDirector : Singleton<MapDirector>
    {
        internal static readonly Dictionary<uint, LocationConnection> IdConnectionLookup = new();
        internal static readonly Dictionary<LocationConnection, uint> ConnectionIdLookup = new();
#if CLIENT
        private static bool _validatedConnectionLookup = false;
#endif
        public override void Setup()
        {
            // Map
            var map_ctr_load = typeof(Map).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(CampaignMode), typeof(XElement) }, null);
            var map_save = typeof(Map).GetMethod(nameof(Map.Save));
            var map_generate = typeof(Map).GetMethod("Generate", BindingFlags.Instance | BindingFlags.NonPublic);
            var map_progressworld_step = AccessTools.Method(typeof(Map), "ProgressWorld", new Type[] { typeof(CampaignMode) });
            var map_progressworld = AccessTools.Method(typeof(Map), "ProgressWorld", new Type[] { typeof(CampaignMode), typeof(CampaignMode.TransitionType), typeof(float) });

            // Leveldata
            var leveldata_ctr_load = typeof(LevelData).GetConstructor(new Type[] { typeof(XElement), typeof(float?), typeof(bool) });
            var leveldata_ctr_generate = typeof(LevelData).GetConstructor(new Type[] { typeof(LocationConnection) });
            var leveldata_save = typeof(LevelData).GetMethod(nameof(LevelData.Save));

            // GameSession
            var gamesession_StartRound = typeof(GameSession).GetMethod(nameof(GameSession.StartRound), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo) });
            var campaignmode_AddExtraMissions = typeof(CampaignMode).GetMethod(nameof(CampaignMode.AddExtraMissions));

            // level generate

            Check(map_ctr_load, "map ctr load");
            Check(map_save, "map_save");
            Check(map_generate, "map_generate");
            Check(map_progressworld_step, "map_progressworld");
            Check(leveldata_ctr_load, "leveldata_ctr_load");
            Check(leveldata_ctr_generate, "leveldata_ctr_generate");
            Check(leveldata_save, "leveldata_save");
            Check(gamesession_StartRound, "gamesession_startround");
            Check(campaignmode_AddExtraMissions, "campaignmode_addextramissions");

            // Map data
            _ = Main.Harmony.Patch(map_ctr_load, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnMapLoad), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(map_generate, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnMapGenerate), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(map_progressworld_step, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(MapDirector.OnProgressWorld_Step))));
            _ = Main.Harmony.Patch(map_progressworld, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), nameof(MapDirector.OnProgressWorld))));

            // Level data
            _ = Main.Harmony.Patch(leveldata_ctr_load, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataLoad), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(leveldata_ctr_generate, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataGenerate), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(leveldata_save, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataSave), BindingFlags.Static | BindingFlags.NonPublic)));

            // Campaign
            _ = Main.Harmony.Patch(campaignmode_AddExtraMissions, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), "OnAddExtraMissions")));
            _ = Main.Harmony.Patch(gamesession_StartRound, new HarmonyMethod(AccessTools.Method(typeof(MapDirector), "OnRoundStart")));
            extraMissions = AccessTools.Field(typeof(CampaignMode), "extraMissions");
            MapLocationEquality = new MapLocationEqualityCheck();

            Modules.Add(new ConstructionMapModule());
            Modules.Add(new DistressMapModule());
        }

        internal MapLocationEqualityCheck MapLocationEquality;

        private void Check(object info, string name)
        {
            if (info == null) Log.Error(name);
        }

        internal FieldInfo extraMissions;
        internal List<MapModule> Modules = new();

        private static void OnRoundStart(GameSession __instance, LevelData levelData)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnRoundStart(levelData);
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

        public static void ForceWorldStep() => OnProgressWorld_Step(GameMain.GameSession.Map);

        private static void OnProgressWorld(Map __instance)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnProgressWorld(__instance);
            }
        }

        private static void OnProgressWorld_Step(Map __instance)
        {
            foreach (var item in Instance.Modules)
            {
                item.OnProgressWorld_Step(__instance);
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
            Log.Debug("Map Load");

            // Generate location connection lookup 
            GenerateConnectionLookup(__instance);
            foreach (var item in Instance.Modules)
            {
                item.OnMapLoad(__instance);
            }
            Instance.MapLocationEquality.CheckEquality();
        }

        private static void OnMapSave()
        {
            Log.Debug("OnMapSave");
        }

        private static void OnMapGenerate(Map __instance)
        {
            Log.Debug("OnMapGenerate:Postfix");
            GenerateConnectionLookup(__instance);
            // Get all starting zone connections
            foreach (var item in Instance.Modules)
            {
                item.OnMapGenerate(__instance);
            }
        }

        // KNOWN ISSUE: On creating a new campaign, all clients will be kicked. Solution: Just load the campaign up again
        private static void GenerateConnectionLookup(Map map)
        {
            MD5 md5 = MD5.Create();
            if (IdConnectionLookup.Count > 0) return;
            foreach (var connection in map.Connections)
            {
                string connectionName = connection.LevelData.Seed;
                uint connectionHash = ToolBox.StringToUInt32Hash(connectionName, md5);
                if (IdConnectionLookup.ContainsKey(connectionHash) || ConnectionIdLookup.ContainsKey(connection)) continue; // skip duplicate entries
                IdConnectionLookup.Add(connectionHash, connection);
                ConnectionIdLookup.Add(connection, connectionHash);
            }
        }
    }

    internal class MapLocationEqualityCheck
    {
        public MapLocationEqualityCheck()
        {
#if CLIENT
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_SENDCLIENT, ConnectionEqualityCheck);
#endif
#if SERVER
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_REQUEST, RequestConnectionEquality);
#endif
        }

        private static bool _validatedConnectionLookup;

        internal void CheckEquality()
        {
            if (!GameMain.IsMultiplayer) return;
#if CLIENT
            if (!_validatedConnectionLookup && GameMain.IsMultiplayer)
            {
                _validatedConnectionLookup = true;
                NetUtil.SendServer(NetUtil.CreateNetMsg(NetEvent.MAP_CONNECTION_EQUALITYCHECK_REQUEST));
            }
#endif
        }

#if CLIENT
        private void ConnectionEqualityCheck(object[] args)
        {
            Log.Debug("Got map connection equality check!");
            IReadMessage inMsg = (IReadMessage)args[0];
            uint connectionCount = inMsg.ReadUInt32();
            if (connectionCount != MapDirector.IdConnectionLookup.Keys.Count)
            {
                KickClient($"The connection lookup generated on your client did not match the one on the server (Client Key Count: {MapDirector.IdConnectionLookup.Keys.Count}, Server Key Count: {connectionCount})");
                return;
            }

            for (int i = 0; i < connectionCount - 1; i++)
            {
                uint key = inMsg.ReadUInt32();
                if (!MapDirector.IdConnectionLookup.ContainsKey(key))
                {
                    KickClient($"The connection lookup generated on your client did not match the one on the server (Client did not contain server key {key})");
                    return;
                }
            }

            Log.Debug("Equality good!");
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
            Log.Debug("Got request for quality check");
            Client c = (Client)args[1];
            if (MapDirector.IdConnectionLookup.Count == 0)
            {
                c.Kick("Client requested equality check too soon!");
                return;
            }
            
            IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.MAP_CONNECTION_EQUALITYCHECK_SENDCLIENT);
            msg.WriteUInt32((uint)MapDirector.IdConnectionLookup.Keys.Count); // write the total count
            foreach (var key in MapDirector.IdConnectionLookup.Keys)
            {
                msg.WriteUInt32(key);
            }

            NetUtil.SendClient(msg, c.Connection);
        }
#endif
    }

    internal class BlackMarketModule
    {

    }

    internal static class MapExtensions
    {
        internal static int GetZoneIndex(this Location location, Map map)
        {
            float zoneWidth = map.Width / MapGenerationParams.Instance.DifficultyZones;
            return MathHelper.Clamp((int)Math.Floor(location.MapPosition.X / zoneWidth) + 1, 1, MapGenerationParams.Instance.DifficultyZones);
        }
    }
}

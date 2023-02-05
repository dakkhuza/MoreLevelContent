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

namespace MoreLevelContent.Shared.Generation
{
    public partial class MapDirector : Singleton<MapDirector>
    {
        public override void Setup()
        {
            // Map
            var map_ctr_load = typeof(Map).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(CampaignMode), typeof(XElement) }, null);
            var map_save = typeof(Map).GetMethod(nameof(Map.Save));
            var map_generate = typeof(Map).GetMethod("Generate", BindingFlags.Instance | BindingFlags.NonPublic);
            var map_progressworld = AccessTools.Method(typeof(Map), "ProgressWorld", new Type[] { });

            // Leveldata
            var leveldata_ctr_load = typeof(LevelData).GetConstructor(new Type[] { typeof(XElement), typeof(float?), typeof(bool) });
            var leveldata_ctr_generate = typeof(LevelData).GetConstructor(new Type[] { typeof(LocationConnection) });
            var leveldata_save = typeof(LevelData).GetMethod(nameof(LevelData.Save));

            // GameSession
            var gamesession_StartRound = typeof(GameSession).GetMethod(nameof(GameSession.StartRound), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo) });
            var campaignmode_AddExtraMissions = typeof(CampaignMode).GetMethod(nameof(CampaignMode.AddExtraMissions));

            Check(map_ctr_load, "map ctr load");
            Check(map_save, "map_save");
            Check(map_generate, "map_generate");
            Check(map_progressworld, "map_progressworld");
            Check(leveldata_ctr_load, "leveldata_ctr_load");
            Check(leveldata_ctr_generate, "leveldata_ctr_generate");
            Check(leveldata_save, "leveldata_save");
            Check(gamesession_StartRound, "gamesession_startround");
            Check(campaignmode_AddExtraMissions, "campaignmode_addextramissions");

            // Map data
            _ = Main.Harmony.Patch(map_ctr_load, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnMapLoad), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(map_generate, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnMapGenerate), BindingFlags.Static | BindingFlags.NonPublic)));
            
            // Level data
            _ = Main.Harmony.Patch(leveldata_ctr_load, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataLoad), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(leveldata_ctr_generate, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataGenerate), BindingFlags.Static | BindingFlags.NonPublic)));
            _ = Main.Harmony.Patch(leveldata_save, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnLevelDataSave), BindingFlags.Static | BindingFlags.NonPublic)));

            // Campaign
            _ = Main.Harmony.Patch(campaignmode_AddExtraMissions, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), "OnAddExtraMissions")));
            _ = Main.Harmony.Patch(gamesession_StartRound, new HarmonyMethod(AccessTools.Method(typeof(MapDirector), "OnRoundStart")));
            extraMissions = AccessTools.Field(typeof(CampaignMode), "extraMissions");

            _ = Main.Harmony.Patch(map_progressworld, postfix: new HarmonyMethod(AccessTools.Method(typeof(MapDirector), "OnProgressWorld")));

            Modules.Add(new ConstructionMapModule());
            Modules.Add(new DistressMapModule());
            // Modules.Add(new )
        }

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
            // Don't do this for the client
            // Client gets save sent by server
            if (Main.IsClient) return;
            // Update a save from before this update to include construction beacons
            // if (!__instance.Connections.Any(c => c.LevelData.MLC().HasBeaconConstruction))
            // {
            //     Log.Debug("Migrating old save...");
            //     for (int i = 0; i < __instance.Connections.Count; i++)
            //     {
            //         var connection = __instance.Connections[i];
            // 
            //         // Skip if there's no beacon station to replace
            //         if (!connection.LevelData.HasBeaconStation) continue;
            // 
            //         // See if we should generate a construction site
            //         var rand = new MTRandom(ToolBox.StringToInt(connection.LevelData.Seed));
            //         LevelData_MLCData extraData = connection.LevelData.MLC();
            //         Instance.TrySpawnBeaconConstruction(connection.LevelData, rand, extraData, connection);
            //     }
            // }

            foreach (var item in Instance.Modules)
            {
                item.OnMapLoad(__instance);
            }
        }

        private static void OnMapSave()
        {
            Log.Debug("OnMapSave");
        }

        // implicitly synced
        private static void OnMapGenerate(Map __instance)
        {
            Log.Debug("OnMapGenerate:Postfix");
            // Get all starting zone connections
            foreach (var item in Instance.Modules)
            {
                item.OnMapGenerate(__instance);
            }
        }
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

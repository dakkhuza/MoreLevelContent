﻿using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using HarmonyLib;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.AI;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.XML;
using System;
using System.Collections.Generic;
using System.Reflection;
using static Barotrauma.CampaignMode;

namespace MoreLevelContent
{
    /// <summary>
    /// Shared
    /// </summary>
    partial class Main : ACsMod
    {
        public static bool IsCampaign => GameMain.GameSession?.Campaign != null || GameMain.IsSingleplayer;
        public static bool IsRunning => GameMain.GameSession?.IsRunning ?? false;

        public static bool IsClient => GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient;
        public const string GUID = "com.dak.mlc";
        public static bool IsRelase = true;
        public static bool IsNightly = false;
        public static bool PreventRoundEnd = false;


        public static Main Instance;
        public static string Version = "0.0.8";
        private static LevelContentProducer levelContentProducer;
        internal static Harmony Harmony;

        public Main()
        {
            Instance = this;
            Log.Debug("Mod Init");
            ConfigManager.Instance.Setup();
            Init();
#if SERVER
            InitServer();
#elif CLIENT
            InitClient();
#endif
        }

        public static void Hook(string name, string hookName, LuaCsFunc hook) => 
            GameMain.LuaCs.Hook.Add(name, hookName, hook, Instance);
        public static void HookMethod(string identifier, MethodInfo method, LuaCsPatch patch, LuaCsHook.HookMethodType hookType) =>
            GameMain.LuaCs.Hook.HookMethod(identifier, method, patch, hookType, Instance);

        public static void Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            Harmony.Patch(original, prefix, postfix, transpiler, finalizer, null);
        }

        public void Init()
        {
            Log.Verbose("Reflecting methods...");
            var level_onCreateWrecks = typeof(Level).GetMethod("CreateWrecks", BindingFlags.NonPublic | BindingFlags.Instance);
            var level_onSpawnNPC = typeof(Level).GetMethod(nameof(Level.SpawnNPCs));
            var level_generate = typeof(Level).GetMethod(nameof(Level.Generate));
            var gameSession_before_startRound = typeof(GameSession).GetMethod(nameof(GameSession.StartRound), new Type[] { typeof(LevelData), typeof(bool), typeof(SubmarineInfo), typeof(SubmarineInfo) });
            var eventManager_TriggerOnEndRoundActions = AccessTools.Method(typeof(GameSession), "EndRound");
            Harmony = new Harmony("com.mlc.dak");

            MoveRuins.Init();
            Hooks.Instance.Setup();
            levelContentProducer = new LevelContentProducer();
            MapDirector.Instance.Setup();
            XMLManager.Instance.Setup();
            InjectionManager.Instance.Setup();
            Commands.Instance.Setup();
            CompatabilityHelper.Instance.Setup();
            ReflectionInfo.Instance.Setup();
            MLCAIObjectiveManager.Instance.Setup();

            if (!levelContentProducer.Active)
            {
                Log.Error("Level content producer is disabled!");
                return;
            }

            Log.Verbose("Hooking...");
            GameMain.LuaCs.Hook.HookMethod(
                "mlc.shared.OnCreateWrecks",
                level_onCreateWrecks,
                OnCreateWrecks,
                LuaCsHook.HookMethodType.After,
                this);

            GameMain.LuaCs.Hook.HookMethod(
                "mlc.shared.OnSpawnNPC",
                level_onSpawnNPC,
                OnSpawnNPC,
                LuaCsHook.HookMethodType.Before,
                this
                );

            GameMain.LuaCs.Hook.HookMethod(
                "mlc.shared.OnLevelGenerate",
                level_generate,
                OnLevelGenerate,
                LuaCsHook.HookMethodType.Before,
                this);

            GameMain.LuaCs.Hook.HookMethod(
                "mlc.shared.before_startRound",
                gameSession_before_startRound,
                OnBeforeStartRound,
                LuaCsHook.HookMethodType.Before,
                this);

            _ = Harmony.Patch(eventManager_TriggerOnEndRoundActions, postfix: new HarmonyMethod(AccessTools.Method(typeof(Main), nameof(Main.OnRoundEnd))));



            Log.Verbose("Done!");
        }


        public object OnCreateWrecks(object self, Dictionary<string, object> args)
        {
            levelContentProducer.CreateWrecks();
            return null;
        }

        public object OnSpawnNPC(object self, Dictionary<string, object> args)
        {
            levelContentProducer.SpawnNPCs();
            return null;
        }

        public object OnLevelGenerate(object self, Dictionary<string, object> args)
        {
            levelContentProducer.LevelGenerate(args["levelData"] as LevelData, args["mirror"] as bool? ?? false);
            MapDirector.Instance.OnLevelGenerate(args["levelData"] as LevelData, args["mirror"] as bool? ?? false);
            return null;
        }

        public object OnBeforeStartRound(object self, Dictionary<string, object> args)
        {
            levelContentProducer.StartRound();
            return null;
        }

        public static void OnRoundEnd(CampaignMode.TransitionType transitionType)
        {
            levelContentProducer.EndRound();
            MapDirector.Instance.RoundEnd(transitionType);
        }

        public override void Stop()
        {
            Harmony.UnpatchAll(); // Cleanup harmony patches
        }
    }
}

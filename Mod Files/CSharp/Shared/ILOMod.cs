using Barotrauma;
using MoreLevelContent.Shared.Generation;
using System.Collections.Generic;
using System.Reflection;

namespace MoreLevelContent.Shared
{
    partial class ILOMod : ACsMod
    {
        public static bool IsCampaign => GameMain.GameSession?.GameMode is MultiPlayerCampaign;
        public static bool IsRunning => GameMain.GameSession?.IsRunning ?? false;
        private LevelContentProducer levelContentProducer;

        public ILOMod() => Init();

        public void Init()
        {
            var level_onCreateWrecks = typeof(Level).GetMethod("CreateWrecks", BindingFlags.NonPublic | BindingFlags.Instance);
            var level_onSpawnNPC = typeof(Level).GetMethod(nameof(Level.SpawnNPCs));
            var level_generate = typeof(Level).GetMethod(nameof(Level.Generate));
            levelContentProducer = new LevelContentProducer();

            if (!levelContentProducer.Active)
            {
                Log.Error("Level content producer is disabled!");
                return;
            }
            GameMain.LuaCs.Hook.HookMethod(
                "mlc_GenerateILO",
                level_onCreateWrecks,
                OnCreateWrecks,
                LuaCsHook.HookMethodType.After,
                this);

            GameMain.LuaCs.Hook.HookMethod(
                "mlc_SpawnNPC",
                level_onSpawnNPC,
                OnSpawnNPC,
                LuaCsHook.HookMethodType.Before,
                this
                );

            GameMain.LuaCs.Hook.HookMethod(
                "mlc_LevelStart",
                level_generate,
                OnLevelGenerate,
                LuaCsHook.HookMethodType.Before,
                this);
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
            foreach (var item in args.Keys)
            {
                Log.Debug( "key: " + item + "value:" + args[item]?.GetType().Name);
            }
            levelContentProducer.LevelGenerate(args["levelData"] as LevelData, args["mirror"] as bool? ?? false);
            return null;
        }

        public override void Stop() { }
    }
}

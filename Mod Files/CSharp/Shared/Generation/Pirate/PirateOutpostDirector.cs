using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.MoreLevelContent.Shared.Config;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Store;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    public class PirateOutpostDirector : GenerationDirector<PirateOutpostDirector>, IGenerateSubmarine, IGenerateNPCs, ILevelStartGenerate, IRoundStatus
    {
        public string ForcedPirateOutpost = "";
        public bool ForceSpawn { get; set; } = false;
        public bool ForceHusk { get; set; } = false;

        public static PirateConfig Config => ConfigManager.Instance.Config.NetworkedConfig.PirateConfig;

        private PirateOutpost _PirateOutpost;

        public override bool Active => PirateStore.HasContent;

        public override void Setup()
        {
            PirateStore.Instance.Setup();
            MethodInfo level_update = AccessTools.Method(typeof(Level), "Update");
            _ = Main.Harmony.Patch(level_update, postfix: new HarmonyMethod(AccessTools.Method(typeof(PirateOutpostDirector), nameof(PirateOutpostDirector.Update))));
        }

        void Update(float deltaTime)
        {
            if (_PirateOutpost != null)
            {
                _PirateOutpost.Update(deltaTime);
            }
        }

        void ILevelStartGenerate.OnLevelGenerationStart(LevelData levelData, bool _)
        {
            _PirateOutpost = null;

            // Prevent an outpost from spawning if the mission is a pirate
            // It will brick the pirates if it does
            if (!Screen.Selected.IsEditor) // Don't check in editor
            {
                foreach (Mission mission in GameMain.GameSession.GameMode!.Missions)
                {
                    if (mission is PirateMission) return;
                }
            }

            var pirateData = levelData.MLC().PirateData;
            if (pirateData.HasPirateOutpost)
            {
                _PirateOutpost = new PirateOutpost(pirateData, ForcedPirateOutpost, levelData.Seed);
                Log.Verbose("Set pirate outpost");
            }
        }

        public void GenerateSub() => _PirateOutpost?.Generate();
        public void SpawnNPCs() => _PirateOutpost?.Populate();
        public void BeforeRoundStart() { }
        public void RoundEnd()
        {
            if (_PirateOutpost != null)
            {
                _PirateOutpost.OnRoundEnd(Level.Loaded.LevelData);
                _PirateOutpost = null;
            }
        }
    }
}

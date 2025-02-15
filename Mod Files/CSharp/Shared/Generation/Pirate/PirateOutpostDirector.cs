using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.MoreLevelContent.Shared.Config;
using Barotrauma.Networking;
using HarmonyLib;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Store;
using MoreLevelContent.Shared.Utils;
using System;
using System.Reflection;

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
            Hooks.Instance.AddUpdateAction(Update);
#if CLIENT
            NetUtil.Register(NetEvent.PIRATEBASE_STATUS, StatusUpdated);
#endif
        }

        internal static void UpdateStatus(PirateData data, LocationConnection con)
        {
#if SERVER
            Log.Debug("Send status");
            var msg = NetUtil.CreateNetMsg(NetEvent.PIRATEBASE_STATUS);
            Int32 id = MapDirector.ConnectionIdLookup[con];
            msg.WriteInt32(id);
            msg.WriteBoolean(data.Revealed);
            msg.WriteInt16((short)data.Status);
            NetUtil.SendAll(msg);
#endif
        }

#if CLIENT
        public void StatusUpdated(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            int conId = inMsg.ReadInt32();
            bool revealed = inMsg.ReadBoolean();
            PirateOutpostStatus status = (PirateOutpostStatus)inMsg.ReadInt16();
            // Look up connection
            var connection = MapDirector.IdConnectionLookup[conId];
            connection.LevelData.MLC().PirateData.Revealed = revealed;
            connection.LevelData.MLC().PirateData.Status = status;
            Log.Debug("Updated pirate status");
        }
#endif

        static void Update(float deltaTime, Camera cam)
        {
            if (Instance._PirateOutpost != null)
            {
                Instance._PirateOutpost.Update(deltaTime);
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

            if (levelData.MLC().PirateData.HasPirateBase)
            {
                _PirateOutpost = new PirateOutpost(levelData.MLC().PirateData, ForcedPirateOutpost, levelData.Seed);
                Log.Verbose("Set pirate outpost");
            }
        }

        public void GenerateSub() => _PirateOutpost?.Generate();
        public void SpawnNPCs() => _PirateOutpost?.Populate();
        public void BeforeRoundStart() { }
        public void RoundEnd()
        {
            Log.Debug("Pirate director round end");
            if (_PirateOutpost != null)
            {
                _PirateOutpost.OnRoundEnd(Level.Loaded.LevelData);
                _PirateOutpost = null;
            } else
            {
                Log.Debug("Pirate outpost not set");
            }
        }
    }
}

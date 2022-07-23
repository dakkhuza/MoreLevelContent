﻿using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.MoreLevelContent.Shared.Config;
using MoreLevelContent;
using MoreLevelContent.Shared;
using Barotrauma.Networking;
using System;

namespace Barotrauma.MoreLevelContent.Config
{
    /// <summary>
    /// Shared
    /// </summary>
    partial class ConfigManager : Singleton<ConfigManager>
    {
        public override void Setup()
        {
#if CLIENT
            LoadConfig();
            SetupClient();
#elif SERVER
            SetupServer();
#endif
        }

        private void LoadConfig()
        {
            if (LuaCsFile.Exists(configFilepath))
            {
                try
                {
                    Config = LuaCsConfig.Load<MLCConfig>(configFilepath);
                    Log.Debug($"Move Chance: {Config.NetworkedConfig.GeneralConfig.RuinMoveChance}");
#if CLIENT
                    DisplayPatchNotes();
#endif
                    Log.Debug(Config.NetworkedConfig.ToString());
                    return;
                } catch
                {
                    Log.Warn("Failed to load config!");
                    DefaultConfig();
                }
            } else
            {
                Log.Debug("File doesn't exist");
                DefaultConfig();
            }
        }

        private void DefaultConfig()
        {
            Log.Debug("Defaulting config...");
            Config = MLCConfig.GetDefault();
#if CLIENT
            SaveConfig(); // Only save the default config on the client, look into changing this for dedicated servers
            DisplayPatchNotes(true);
#endif
        }

        private void SaveConfig()
        {
            LuaCsConfig.Save(configFilepath, Config);
            Log.Debug("Saved config to disk!");
        }

        private void ReadNetConfig(ref IReadMessage inMsg)
        {
            try
            {
                Config.NetworkedConfig = INetSerializableStruct.Read<NetworkedConfig>(inMsg);
            } catch(Exception err)
            {
                Log.Debug(err.ToString());
            }
        }

        private void WriteConfig(ref IWriteMessage outMsg) => 
            (Config.NetworkedConfig as INetSerializableStruct).Write(outMsg);

        private static readonly string configFilepath = $"{ACsMod.GetSoreFolder<Main>()}/MLCConfig.xml";
        public MLCConfig Config;
    }
}

using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.MoreLevelContent.Shared.Config;
using MoreLevelContent;
using MoreLevelContent.Shared;
using Barotrauma.Networking;

namespace Barotrauma.MoreLevelContent.Config
{
    /// <summary>
    /// Shared
    /// </summary>
    partial class ConfigManager : Singleton<ConfigManager>
    {
        public override void Setup()
        {
            LoadConfig();
#if CLIENT
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
                    config = LuaCsConfig.Load<MLCConfig>(configFilepath);
                    return;
                } catch
                {
                    Log.Warn("Failed to load config!");
                    DefaultConfig();
                }
            } else
            {
                DefaultConfig();
            }
        }

        private void DefaultConfig()
        {
            Log.Debug("Defaulting config...");
            config = MLCConfig.GetDefault();
            SaveConfig();
        }

        private void SaveConfig()
        {
            LuaCsConfig.Save(configFilepath, Config);
            Log.Debug("Saved config to disk!");
        }

        private void ReadNetConfig(ref IReadMessage inMsg)
        {
            config.Pirate = PirateConfig.ReadFrom(ref inMsg);
            Log.Verbose(Config.ToString());
        }

        private static readonly string configFilepath = $"{ACsMod.GetSoreFolder<Main>()}/MLCConfig.xml";
        public MLCConfig Config => config;
        private MLCConfig config;
    }
}

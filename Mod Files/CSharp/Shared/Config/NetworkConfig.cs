using Barotrauma.Networking;
using System;

namespace Barotrauma.MoreLevelContent.Shared.Config
{
    [NetworkSerialize]
    public struct NetworkedConfig : INetSerializableStruct
    {
        [NetworkSerialize]
        public PirateConfig PirateConfig;

        [NetworkSerialize]
        public LevelConfig GeneralConfig;

        public static NetworkedConfig GetDefault()
        {
            NetworkedConfig config = new NetworkedConfig
            {
                PirateConfig = PirateConfig.GetDefault(),
                GeneralConfig = LevelConfig.GetDefault()
            };
            return config;
        }

        public override string ToString() => PirateConfig.ToString() + GeneralConfig.ToString();
    }
}
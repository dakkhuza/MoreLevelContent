using Barotrauma.Networking;
using MoreLevelContent;
using System;
using System.Collections.Generic;
using System.Text;

namespace Barotrauma.MoreLevelContent.Shared.Config
{
    public struct MLCConfig
    {
        public NetworkedConfig NetworkedConfig;
        public ClientConfig Client;
        public string Version;

        public static MLCConfig GetDefault()
        {
            MLCConfig config = new MLCConfig
            {
                NetworkedConfig = NetworkedConfig.GetDefault(),
                Client = ClientConfig.GetDefault(),
                Version = Main.Version
            };
            return config;
        }

        public override string ToString() =>
            $"\n_= MLC CONFIG =_" + NetworkedConfig.ToString() + Client.ToString();
    }
}

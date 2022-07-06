using Barotrauma.Networking;
using System;
using System.Collections.Generic;
using System.Text;

namespace Barotrauma.MoreLevelContent.Shared.Config
{
    public struct MLCConfig
    {
        public PirateConfig Pirate;
        public DebugConfig Debug;

        public static MLCConfig GetDefault()
        {
            MLCConfig config = new MLCConfig
            {
                Pirate = PirateConfig.GetDefault(),
                Debug = DebugConfig.GetDefault()
            };
            return config;
        }

        public void WriteTo(ref IWriteMessage outMsg) => Pirate.WriteTo(ref outMsg);

        public override string ToString() =>
            $"\n_= MLC CONFIG =_" + Pirate.ToString() + Debug.ToString();
    }
}

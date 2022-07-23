

using Barotrauma.Networking;

namespace Barotrauma.MoreLevelContent.Shared.Config
{
    [NetworkSerialize]
    public struct LevelConfig : INetSerializableStruct
    {
        // public bool MoveRuins;
        public int RuinMoveChance;

        public static LevelConfig GetDefault()
        {
            LevelConfig config = new LevelConfig
            {
                RuinMoveChance = 25
            };
            return config;
        }

        public override string ToString() =>
            $"\n-General Config-\n" +
            $"RuinMoveChance: {RuinMoveChance}";
    }
}
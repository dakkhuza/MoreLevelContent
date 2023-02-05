using Barotrauma.Networking;
using Barotrauma;
using MoreLevelContent.Shared;

namespace MoreLevelContent.Missions
{
    // Client
    partial class MissionNPCCollection
    {
        internal void Read(IReadMessage msg)
        {
            bool hasCharacters = msg.ReadBoolean();
            if (!hasCharacters)
            {
                Log.Debug("Mission has no characters");
                return;
            }
            byte characterCount = msg.ReadByte();
            for (int i = 0; i < characterCount; i++)
            {
                Character character = Character.ReadSpawnData(msg);
                bool allowOrdering = msg.ReadBoolean();
                characters.Add(character);
                if (allowOrdering)
                {
                    _ = GameMain.GameSession.CrewManager.AddCharacterToCrewList(character);
                    Log.InternalDebug($"Added character {character.Name} to crew list");
                }
                ushort itemCount = msg.ReadUInt16();
                for (int j = 0; j < itemCount; j++)
                {
                    Item.ReadSpawnData(msg);
                }
            }
            if (characters.Contains(null))
            {
                throw new System.Exception("Error in EscortMission.ClientReadInitial: character list contains null (mission: " + mission.Prefab.Identifier + ")");
            }

            if (characters.Count != characterCount)
            {
                throw new System.Exception("Error in EscortMission.ClientReadInitial: character count does not match the server count (" + characterCount + " != " + characters.Count + "mission: " + mission.Prefab.Identifier + ")");
            }

            InitCharacters();
        }
    }
}

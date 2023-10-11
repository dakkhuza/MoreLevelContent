using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma.MoreLevelContent.Shared.Utils
{
    public static class CharacterUtils
    {
        internal static Character CreateHuman(HumanPrefab humanPrefab, List<Character> characters, Dictionary<Character, List<Item>> characterItems, Submarine submarine, CharacterTeamType teamType, ISpatialEntity positionToStayIn = null)
        {
            CharacterInfo characterInfo = humanPrefab.CreateCharacterInfo(Rand.RandSync.ServerAndClient);
            characterInfo.TeamID = teamType;

            if (positionToStayIn == null)
            {
                positionToStayIn =
                    WayPoint.GetRandom(SpawnType.Human, characterInfo.Job?.Prefab, submarine) ??
                    WayPoint.GetRandom(SpawnType.Human, null, submarine);
            }

            Character spawnedCharacter = Character.Create(characterInfo.SpeciesName, positionToStayIn.WorldPosition, ToolBox.RandomSeed(8), characterInfo);
            spawnedCharacter.HumanPrefab = humanPrefab;
            humanPrefab.InitializeCharacter(spawnedCharacter, positionToStayIn);
            humanPrefab.GiveItems(spawnedCharacter, submarine, null, Rand.RandSync.ServerAndClient);

            characters.Add(spawnedCharacter);
            characterItems.Add(spawnedCharacter, spawnedCharacter.Inventory.FindAllItems(recursive: true));

            return spawnedCharacter;
        }

        internal static Character CreateHuman(HumanPrefab humanPrefab, List<Character> characters, Dictionary<Character, List<Item>> characterItems, CharacterTeamType teamType, Vector2 spawnPosition, bool createNetEvent = true)
        {
            CharacterInfo characterInfo = humanPrefab.CreateCharacterInfo(Rand.RandSync.ServerAndClient);
            characterInfo.TeamID = teamType;
            Character spawnedCharacter = Character.Create(characterInfo.SpeciesName, spawnPosition, ToolBox.RandomSeed(8), characterInfo, createNetworkEvent: createNetEvent);
            spawnedCharacter.HumanPrefab = humanPrefab;
            humanPrefab.InitializeCharacter(spawnedCharacter);
            humanPrefab.GiveItems(spawnedCharacter, null, null, Rand.RandSync.ServerAndClient, createNetworkEvents: createNetEvent);

            characters.Add(spawnedCharacter);
            characterItems.Add(spawnedCharacter, spawnedCharacter.Inventory.FindAllItems(recursive: true));

            return spawnedCharacter;
        }

        internal static HumanPrefab GetHumanPrefabFromElement(XElement element)
        {
            if (element.Attribute("name") != null)
            {
                DebugConsole.ThrowError("Error");

                return null;
            }

            Identifier characterIdentifier = element.GetAttributeIdentifier("identifier", Identifier.Empty);
            Identifier characterFrom = element.GetAttributeIdentifier("from", Identifier.Empty);
            HumanPrefab humanPrefab = NPCSet.Get(characterFrom, characterIdentifier);
            if (humanPrefab == null)
            {
                DebugConsole.ThrowError("Couldn't spawn character for mission: character prefab \"" + characterIdentifier + "\" not found");
                return null;
            }

            return humanPrefab;
        }

        public static XElement GetRandomDifficultyModifiedElement(XElement parentElement, float levelDifficulty, float randomnessModifier, Random rand)
        {
            // look for the element that is closest to our difficulty, with some randomness
            XElement bestElement = null;
            float bestValue = float.MaxValue;
            foreach (XElement element in parentElement.Elements())
            {
                float applicabilityValue = GetDifficultyModifiedValue(element.GetAttributeFloat(0f, "preferreddifficulty"), levelDifficulty, randomnessModifier, rand);
                if (applicabilityValue < bestValue)
                {
                    bestElement = element;
                    bestValue = applicabilityValue;
                }
            }
            return bestElement;
        }

        private static float GetDifficultyModifiedValue(float preferredDifficulty, float levelDifficulty, float randomnessModifier, Random rand) => Math.Abs(levelDifficulty - preferredDifficulty + MathHelper.Lerp(-randomnessModifier, randomnessModifier, (float)rand.NextDouble()));

    }
}

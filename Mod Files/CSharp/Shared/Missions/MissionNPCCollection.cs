using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.Networking;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MoreLevelContent.Missions
{
    // Shared
    partial class MissionNPCCollection
    {
        internal readonly List<Character> characters = new();
        internal readonly Dictionary<Character, List<Item>> characterItems = new();

        internal bool AllHumansAlive => characters.All(c => IsAlive(c));
        internal bool AnyHumanAlive => characters.Any(c => IsAlive(c));
        internal bool AnyHumanSurvived => characters.Any(c => Survived(c));
        internal int AliveHumans => characters.Where(c => IsAlive(c)).Count();
        internal delegate void OnCharacterCreated(Character character, XElement missionCharacterConfig);
        internal Character this[int index] => characters[index];

        internal MissionNPCCollection(Mission mission, XElement characterConfig)
        {
            this.mission = mission;
            this.characterConfig = characterConfig;
        }
        private readonly Mission mission;
        private readonly XElement characterConfig;


        public void Clear()
        {
            characters.Clear();
            characterItems.Clear();
        }

        public void End(bool completed)
        {
            foreach (Character character in characters)
            {
                if (character.Inventory == null) { continue; }
                foreach (Item item in character.Inventory.AllItemsMod)
                {
                    //item didn't spawn with the characters -> drop it
                    if (!characterItems.Any(c => c.Value.Contains(item)))
                    {
                        item.Drop(character);
                    }
                }
            }

            // characters that survived will take their items with them, in case players tried to be crafty and steal them
            // this needs to run here in case players abort the mission by going back home
            foreach (var characterItem in characterItems)
            {
                if (Survived(characterItem.Key) || !completed)
                {
                    foreach (Item item in characterItem.Value)
                    {
                        if (!item.Removed)
                        {
                            item.Remove();
                        }
                    }
                }
            }
        }
    

        internal void CreateHumansInSubmarine(Submarine submarine, CharacterTeamType team = CharacterTeamType.FriendlyNPC, OnCharacterCreated  onCharacterCreated = null)
        {
            if (characterConfig == null)
            {
                Log.Warn("No characters");
                return;
            }
            WayPoint explicitStayInHullPos = WayPoint.GetRandom(SpawnType.Human, null, submarine);
            Rand.RandSync randSync = Rand.RandSync.Unsynced;
            List<(HumanPrefab prefab, XElement config)> humanPrefabsToSpawn = new List<(HumanPrefab, XElement)>();
            foreach (XElement element in characterConfig.Elements())
            {
                var humanPrefab = GetHumanPrefabFromElement(element);
                humanPrefabsToSpawn.Add((humanPrefab, element));
            }
            foreach (var (prefab, config) in humanPrefabsToSpawn)
            {
                var humanPrefab = prefab;
                XElement characterSpecificConfig = config;
                if (humanPrefab == null || humanPrefab.Job.IsEmpty || humanPrefab.Job == "any") { continue; }
                var jobPrefab = humanPrefab.GetJobPrefab(randSync);
                var stayPos = explicitStayInHullPos;
                if (jobPrefab != null)
                {
                    stayPos = WayPoint.GetRandom(SpawnType.Human, jobPrefab, submarine) ?? explicitStayInHullPos;
                }
                XElement additionalItemsElement = config?.GetChildElement("additionalitems");
                ContentXElement additionalItems = new ContentXElement(null, additionalItemsElement);

                Character spawnedCharacter = CreateHuman(humanPrefab, characters, characterItems, submarine, team, stayPos, additionalItems: additionalItemsElement != null ? additionalItems.Elements() : null);
                spawnedCharacter.EnableDespawn = false; // don't let mission npcs despawn
                spawnedCharacter.GiveIdCardTags(stayPos, false);
                onCharacterCreated?.Invoke(spawnedCharacter, characterSpecificConfig);
                spawnedCharacter.MLC().NPCElement = characterSpecificConfig;
#if CLIENT
                if (GameMain.IsSingleplayer)
                {
                    if (characterSpecificConfig.GetAttributeBool("allowordering", false))
                    {
                        _ = GameMain.GameSession.CrewManager.AddCharacterToCrewList(spawnedCharacter);
                    }
                }
#endif
            }
            Log.Debug("end");
            InitCharacters();
        }

        internal Character CreateHuman(HumanPrefab humanPrefab, List<Character> characters, Dictionary<Character, List<Item>> characterItems, Submarine submarine, CharacterTeamType teamType, ISpatialEntity positionToStayIn = null, bool giveTags = true, IEnumerable<ContentXElement> additionalItems = null)
        {
            var characterInfo = humanPrefab.CreateCharacterInfo(Rand.RandSync.Unsynced);
            characterInfo.TeamID = teamType;

            if (positionToStayIn == null)
            {
                positionToStayIn =
                    WayPoint.GetRandom(SpawnType.Human, characterInfo.Job?.Prefab, submarine) ??
                    WayPoint.GetRandom(SpawnType.Human, null, submarine);
            }
            Character spawnedCharacter = Character.Create(characterInfo.SpeciesName, positionToStayIn.WorldPosition, ToolBox.RandomSeed(8), characterInfo, createNetworkEvent: false);
            spawnedCharacter.HumanPrefab = humanPrefab;
            humanPrefab.InitializeCharacter(spawnedCharacter, positionToStayIn);
            _ = humanPrefab.GiveItems(spawnedCharacter, submarine, null, createNetworkEvents: false);
            foreach (var item in spawnedCharacter.Inventory.AllItems)
            {
                IdCard card = item.GetComponent<IdCard>();
                if (card == null) continue;
                card.SubmarineSpecificID = submarine.SubmarineSpecificIDTag;
            }

            if (additionalItems != null)
            {
                foreach (var additionalItem in additionalItems)
                {
                    int amount = additionalItem.GetAttributeInt("amount", 1);
                    for (int i = 0; i < amount; i++)
                    {
                        HumanPrefab.InitializeItem(spawnedCharacter, additionalItem, submarine, humanPrefab, createNetworkEvents: false);
                    }
                }
            }
            characters.Add(spawnedCharacter);
            characterItems.Add(spawnedCharacter, spawnedCharacter.Inventory.FindAllItems(recursive: true));
            
            return spawnedCharacter;
        }

        internal void GiveCharacterItem(Character character, ContentXElement itemElement, bool createNetworkEvents = true)
        {
            HumanPrefab.InitializeItem(character, itemElement, null, character.HumanPrefab, createNetworkEvents: createNetworkEvents);
            characterItems[character] = character.Inventory.FindAllItems(recursive: true);
        }

        internal void CreateHumansAtPosition(CharacterTeamType team, Vector2 position, OnCharacterCreated onCharacterCreated)
        {
            List<(HumanPrefab, XElement)> humanPrefabsToSpawn = new List<(HumanPrefab, XElement)>();
            foreach (XElement element in characterConfig?.Elements())
            {
                var humanPrefab = GetHumanPrefabFromElement(element);
                humanPrefabsToSpawn.Add((humanPrefab, element));
            }

            foreach (var prefabToSpawn in humanPrefabsToSpawn)
            {
                var humanPrefab = prefabToSpawn.Item1;
                XElement characterMissionConfig = prefabToSpawn.Item2;
                Character character = CreateHumanAtPosition(humanPrefab, team, position);
                character.EnableDespawn = false; // don't let mission npcs despawn
                onCharacterCreated.Invoke(character, characterMissionConfig);
                character.MLC().NPCElement = characterMissionConfig;
            }
            InitCharacters();
        }

        internal Character CreateHumanAtPosition(HumanPrefab humanPrefab, CharacterTeamType team, Vector2 spawnPosition)
        {
            Character character = CharacterUtils.CreateHuman(humanPrefab, characters, characterItems, team, spawnPosition, false);
            return character;
        }

        private HumanPrefab GetHumanPrefabFromElement(XElement element)
        {
            if (element.Attribute("name") != null)
            {
                DebugConsole.ThrowError("Error in mission \"" + mission.Prefab.Identifier + "\" - use character identifiers instead of names to configure the characters.");

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

        private void InitCharacters()
        {
            Log.Debug("characterCount");
            int i = 0;
            foreach (XElement element in characterConfig.Elements())
            {
                characters[i].IsEscorted = false;
                Color col = element.GetAttributeColor("color", Color.LightGreen);
                characters[i].UniqueNameColor = col;
                Log.Debug($"Set color to {col} for character {characters[i].Name}");
                i++;
            }
        }

        internal static bool IsAlive(Character character) => character != null && !character.Removed && !character.IsDead;

        internal static bool IsCaptured(Character character) => character.LockHands;

        internal static bool Survived(Character character)
        {
            return IsAlive(character) && character.CurrentHull?.Submarine != null &&
                (character.CurrentHull.Submarine == Submarine.MainSub || Submarine.MainSub.DockedTo.Contains(character.CurrentHull.Submarine));
        }

        internal static bool Close(Character target, Character ai, float closeDistance)
        {
            float dist = Vector2.DistanceSquared(ai.WorldPosition, target.WorldPosition);
            return dist < closeDistance * closeDistance;
        }

        internal static Character GetClosest(Character ai)
        {
            float closest = float.MaxValue;
            Character target = null;
            foreach (Character player in Character.CharacterList.Where(c => c.IsPlayer))
            {
                if (player.IsDead) continue; // skip dead players
                float dist = Vector2.DistanceSquared(player.WorldPosition, ai.WorldPosition);
                if (dist < closest)
                {
                    closest = dist;
                    target = player;
                }
            }
            return target;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Store;
using MoreLevelContent.Shared.Utils;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    public class PirateOutpost
    {
        private readonly List<Character> characters;
        private readonly Dictionary<Character, List<Item>> characterItems;
        private readonly PirateNPCSetDef selectedPirateSet;
        private readonly PirateOutpostDef selectedOutpost;
        private readonly float pirateDiff;
        private Submarine enemyBase;
        readonly PirateSpawnData _spawnData;

        public PirateOutpost(PirateSpawnData spawnData)
        {
            characters = new List<Character>();
            characterItems = new Dictionary<Character, List<Item>>();
            selectedPirateSet = PirateStore.Instance.GetNPCSetForDiff(spawnData.PirateDifficulty);
            selectedOutpost = PirateStore.Instance.GetPirateOutpostForDiff(spawnData.PirateDifficulty);
            Log.Debug($"Selected NPC set {selectedPirateSet.Prefab.Name}");
            Log.Debug($"Selected outpost {selectedOutpost.ContentFile.Path}");
            pirateDiff = spawnData.PirateDifficulty;
            _spawnData = spawnData;
        }

        public void Generate()
        {
            characters.Clear();
            characterItems.Clear();

            enemyBase = PirateOutpostDirector.Instance.SpawnSubOnPath(Level.Loaded, "Pirate Outpost", selectedOutpost.ContentFile);
            enemyBase.Info.DisplayName = "Pirate Base";
            enemyBase.ShowSonarMarker = false; // Don't show the base on sonar

            if (enemyBase.GetItems(alsoFromConnectedSubs: false).Find(i => i.HasTag("reactor") && !i.NonInteractable)?.GetComponent<Reactor>() is Reactor reactor)
            {
                reactor.PowerUpImmediately();
            }

            enemyBase.TeamID = CharacterTeamType.None;
            Log.InternalDebug($"Spawned a pirate base with name {enemyBase.Info.Name}");
        }

        public void Populate()
        {
            bool commanderAssigned = false;
            Log.InternalDebug("Spawning Pirates");

            if (enemyBase == null)
            {
                Log.Error("Pirate outpost was null! Aborting NPC spawn...");
                return;
            }

            XElement characterConfig = selectedPirateSet.Prefab.ConfigElement.GetChildElement("Characters");
            XElement characterTypeConfig = selectedPirateSet.Prefab.ConfigElement.GetChildElement("CharacterTypes");
            float addedMissionDifficultyPerPlayer = selectedPirateSet.Prefab.ConfigElement.GetAttributeFloat("addedmissiondifficultyperplayer", 0);

            int playerCount = 1;

#if SERVER
            playerCount = GameMain.Server.ConnectedClients.Where(c => !c.SpectateOnly || !GameMain.Server.ServerSettings.AllowSpectating).Count();
#endif

            float enemyCreationDifficulty = pirateDiff + (playerCount * addedMissionDifficultyPerPlayer);
            Random rand = new MTRandom(ToolBox.StringToInt(Level.Loaded.Seed));

            foreach (XElement element in characterConfig.Elements())
            {
                // it is possible to get more than the "max" amount of characters if the modified difficulty is high enough; this is intentional
                // if necessary, another "hard max" value could be used to clamp the value for performance/gameplay concerns
                int amountCreated = GetDifficultyModifiedAmount(element.GetAttributeInt("minamount", 0), element.GetAttributeInt("maxamount", 0), enemyCreationDifficulty, rand);
                for (int i = 0; i < amountCreated; i++)
                {
                    XElement characterType =
                        characterTypeConfig.Elements()
                        .Where(e => e.GetAttributeString("typeidentifier", string.Empty) == element.GetAttributeString("typeidentifier", string.Empty))
                        .FirstOrDefault();

                    if (characterType == null)
                    {
                        DebugConsole.NewMessage($"No characters defined in the loaded XML!!");
                        return;
                    }

                    //TODO: Varient elements don't seem to work
                    XElement variantElement = CharacterUtils.GetRandomDifficultyModifiedElement(characterType, pirateDiff, 25f, rand);
                    if (variantElement == null)
                    {
                        Log.Error("Varient element was null!");
                        continue;
                    }

                    HumanPrefab character = CharacterUtils.GetHumanPrefabFromElement(variantElement);
                    if (character == null)
                    {
                        Log.Debug(characterType.ToString());
                        Log.Debug(variantElement.ToString());
                        Log.Error("Character was null!!");
                        continue;
                    }

                    Character spawnedCharacter = CharacterUtils.CreateHuman(character, characters, characterItems, enemyBase, CharacterTeamType.None, null);
                    if (!commanderAssigned)
                    {
                        bool isCommander = variantElement.GetAttributeBool("iscommander", false);
                        if (isCommander && spawnedCharacter.AIController is HumanAIController humanAIController)
                        {
                            humanAIController.InitShipCommandManager();
                            commanderAssigned = true;
                            Log.Debug("Spawned Commader");
                        }
                    }

                    foreach (Item item in spawnedCharacter.Inventory.AllItems)
                    {
                        if (item?.Prefab.Identifier == "idcard")
                        {
                            item.AddTag("id_pirate");
                        }
                    }
                }
            }

            HuskOutpost();
        }

        private void HuskOutpost()
        {
            if (!_spawnData.Husked) return;
            Log.Debug("You've met with a terrible fate, haven't you?");
            if (!AfflictionHelper.TryGetAffliction("huskinfection", out AfflictionPrefab husk))
            {
                Log.Error("Couldn't get the husk affliction!!!");
            }

            foreach (Character character in characters)
            {
                character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, new Affliction(husk, 200));
                character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, new Affliction(AfflictionPrefab.InternalDamage, 100));
            }
        }

        private int GetDifficultyModifiedAmount(int minAmount, int maxAmount, float levelDifficulty, Random rand) => 
            Math.Max(
                (int)Math.Round(
                    minAmount + 
                    ((maxAmount - minAmount) * (levelDifficulty + MathHelper.Lerp(-25, 25, (float)rand.NextDouble())) / 100)
                    ), 
                minAmount);

    }
}

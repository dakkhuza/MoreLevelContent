using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Store;
using MoreLevelContent.Shared.Utils;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    public class PirateOutpost
    {
        private readonly List<Character> characters;
        private readonly Dictionary<Character, List<Item>> characterItems;
        private readonly PirateNPCSetDef selectedPirateSet;
        private readonly PirateOutpostDef _SelectedSubmarine;
        private readonly float _Difficulty;
        private Submarine _Sub;
        private Character _Commander;
        readonly PirateData _Data;
        private bool _Generated = false;

        public PirateOutpost(PirateData data, string filePath)
        {
            characters = new List<Character>();
            characterItems = new Dictionary<Character, List<Item>>();
            selectedPirateSet = PirateStore.Instance.GetNPCSetForDiff(data.Difficulty);

            _SelectedSubmarine = filePath.IsNullOrEmpty()
                ? PirateStore.Instance.GetPirateOutpostForDiff(data.Difficulty)
                : PirateStore.Instance.FindOutpostWithPath(filePath);

            Log.Verbose($"Selected NPC set {selectedPirateSet.Prefab.Name}");
            Log.Verbose($"Selected outpost {_SelectedSubmarine.SubInfo.FilePath}");
            _Difficulty = data.Difficulty;
            _Data = data;
        }

        public void Generate()
        {
            if (_Generated) return;
            _Generated = true;
            characters.Clear();
            characterItems.Clear();

            _Sub = PirateOutpostDirector.Instance.SpawnSubOnPath("Pirate Outpost", _SelectedSubmarine.SubInfo.FilePath, ignoreCrushDepth: true, placementType: _SelectedSubmarine.PlacementType);
            if (_Sub == null)
            {
                Log.Error("Failed to place pirate outpost! Skipping...");
                return;
            }
            _Sub.PhysicsBody.BodyType = FarseerPhysics.BodyType.Static;
            _Sub.Info.DisplayName = TextManager.Get("mlc.pirateoutpost");
            _Sub.ShowSonarMarker = PirateOutpostDirector.Config.DisplaySonarMarker;
            _Sub.TeamID = CharacterTeamType.None;
            _Sub.Info.Type = SubmarineType.EnemySubmarine;


            if (_Data.Status == PirateOutpostStatus.Active)
            {
                SetupActive();
            } else
            {
                SetupDestroyed();
            }

            Log.InternalDebug($"Spawned a pirate base with name {_Sub.Info.Name}");
        }

        void SetupActive()
        {
            if (_Sub.GetItems(alsoFromConnectedSubs: false).Find(i => i.HasTag("reactor") && !i.NonInteractable)?.GetComponent<Reactor>() is Reactor reactor)
            {
                reactor.PowerUpImmediately();
                reactor.FuelConsumptionRate = 0; // never run out of fuel
                // Make sure the reactor doesn't explode lol
                if (CompatabilityHelper.Instance.HazardousReactorsInstalled) reactor.Item.InvulnerableToDamage = true;
            }
        }

        void SetupDestroyed()
        {
            _Sub.Info.IsManuallyOutfitted = true;
            // new Explosion(500, 0, 0, 200, 200, 100).Explode()
        }

        public void Populate()
        {
            // Don't spawn crew on destroyed outposts
            if (_Data.Status == PirateOutpostStatus.Destroyed) return;
            bool commanderAssigned = false;
            Log.InternalDebug("Spawning Pirates");

            if (_Sub == null)
            {
                Log.Error("Pirate outpost was null! Aborting NPC spawn...");
                return;
            }

            // Don't spawn more pirates than there are diving suits on the outpost
            int maxSpawns = Item.ItemList.Where(it => it.Submarine == _Sub && (it.Prefab.Identifier == "divingsuitlocker2" || it.Prefab.Identifier == "divingsuitlocker")).Count();
            int currentSpawns = 0;

            XElement characterConfig = selectedPirateSet.Prefab.ConfigElement.GetChildElement("Characters");
            XElement characterTypeConfig = selectedPirateSet.Prefab.ConfigElement.GetChildElement("CharacterTypes");
            float addedMissionDifficultyPerPlayer = selectedPirateSet.Prefab.ConfigElement.GetAttributeFloat("addedmissiondifficultyperplayer", 0);

            int playerCount = 1;

#if SERVER
            playerCount = GameMain.Server.ConnectedClients.Where(c => !c.SpectateOnly || !GameMain.Server.ServerSettings.AllowSpectating).Count();
#endif
            if (!PirateOutpostDirector.Config.AddDiffPerPlayer) addedMissionDifficultyPerPlayer = 0;

            float enemyCreationDifficulty = _Difficulty + (playerCount * addedMissionDifficultyPerPlayer);
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
                    XElement variantElement = CharacterUtils.GetRandomDifficultyModifiedElement(characterType, _Difficulty, 25f, rand);
                    if (variantElement == null)
                    {
                        Log.Error("Varient element was null!");
                        continue;
                    }

                    bool isCommander = variantElement.GetAttributeBool("iscommander", false);
                    // don't spawn more than the max diving suits on the outpost
                    if (currentSpawns > maxSpawns && (commanderAssigned || isCommander)) break;

                    HumanPrefab character = CharacterUtils.GetHumanPrefabFromElement(variantElement);
                    if (character == null)
                    {

                        Log.Error($"Character was null!\nTYPE: {characterType}, VARIANT: {variantElement}");
                        continue;
                    }

                    Character spawnedCharacter = CharacterUtils.CreateHuman(character, characters, characterItems, _Sub, CharacterTeamType.None, null);
                    if (!commanderAssigned)
                    {
                        if (isCommander && spawnedCharacter.AIController is HumanAIController humanAIController)
                        {
                            humanAIController.InitShipCommandManager();
                            commanderAssigned = true;
                            _Commander = spawnedCharacter;
                            Log.Verbose("Spawned Commader");
                        }
                    }

                    

                    foreach (Item item in spawnedCharacter.Inventory.AllItems)
                    {
                        if (item?.Prefab.Identifier == "idcard")
                        {
                            item.AddTag("id_pirate");
                        }
                    }
                    currentSpawns++;
                }
            }

            HuskOutpost();
        }

        void PopulateDestroyed()
        {

        }

        internal void OnRoundEnd(LevelData levelData)
        {
            var success = GameMain.GameSession.CrewManager.GetCharacters().Any(c => !c.IsDead);
            if (!success) return;

            // If more than half of the crew or the commander is dead, the outpost is destroyed
            if (characters.Select(c => c.IsDead || c.Removed).Count() > characters.Count / 2 || (_Commander.IsDead || _Commander.Removed))
            {
                levelData.MLC().PirateData.Status = PirateOutpostStatus.Destroyed;
            }
        }

        private void HuskOutpost()
        {
            if (!_Data.Husked) return;
            Log.InternalDebug("You've met with a terrible fate, haven't you?");
            if (!AfflictionHelper.TryGetAffliction("huskinfection", out AfflictionPrefab husk))
            {
                Log.Error("Couldn't get the husk affliction!!!");
                return;
            }

            foreach (Character character in characters)
            {
                character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, new Affliction(husk, 200));
                character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, new Affliction(AfflictionPrefab.InternalDamage, 99));
                character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, new Affliction(AfflictionPrefab.Stun, 100));
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

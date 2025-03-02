﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Store;
using MoreLevelContent.Shared.Utils;
using static Barotrauma.Level;

namespace MoreLevelContent.Shared.Generation.Pirate
{
    internal class PirateOutpost
    {
        public PirateBaseRelationStatus Status { get; private set; }
        private readonly List<Character> characters;
        private readonly Dictionary<Character, List<Item>> characterItems;
        private readonly PirateNPCSetDef selectedPirateSet;
        private readonly PirateOutpostDef _SelectedSubmarine;
        private readonly float _Difficulty;
        private Submarine _Sub;
        private Character _Commander;
        readonly PirateData _Data;
        private bool _Generated = false;
        private bool _Revealed = false;
        

        public PirateOutpost(PirateData data, string filePath, string seed)
        {
            characters = new List<Character>();
            characterItems = new Dictionary<Character, List<Item>>();
            selectedPirateSet = PirateStore.Instance.GetNPCSetForDiff(data.Difficulty, seed);

            _SelectedSubmarine = filePath.IsNullOrEmpty()
                ? PirateStore.Instance.GetPirateOutpostForDiff(data.Difficulty, seed)
                : PirateStore.Instance.FindOutpostWithPath(filePath);

            Log.Verbose($"Selected NPC set {selectedPirateSet.Prefab.Name}");
            Log.Verbose($"Selected outpost {_SelectedSubmarine.SubInfo.FilePath}");
            _Difficulty = data.Difficulty;
            _Data = data;
            _Revealed = data.Revealed;
        }

        public void Update(float deltaTime)
        {
            if (Main.IsClient || _Revealed) return;
            if (_Sub == null) return;
            float minDist = Sonar.DefaultSonarRange / 2f;
            foreach (Submarine submarine in Submarine.Loaded)
            {
                if (submarine.Info.Type != SubmarineType.Player) { continue; }
                if (Vector2.DistanceSquared(submarine.WorldPosition, _Sub.WorldPosition) < minDist * minDist)
                {
                    _Revealed = true;
                    Log.Debug("Revealed pirate base");
                    break;
                }
            }
            foreach (Character c in Character.CharacterList)
            {
                if (c != Character.Controlled && !c.IsRemotePlayer) { continue; }
                if (Vector2.DistanceSquared(c.WorldPosition, _Sub.WorldPosition) < minDist * minDist)
                {
                    _Revealed = true;
                    Log.Debug("Revealed pirate base");
                    break;
                }
            }
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

            if (CompatabilityHelper.Instance.DynamicEuropaInstalled)
            {
                SetupDE();
            } else
            {
                Status = PirateBaseRelationStatus.Hostile;
            }

            _Sub.TeamID = CharacterTeamType.None;
            _Sub.Info.Type = SubmarineType.EnemySubmarine;

            switch (Status)
            {
                case PirateBaseRelationStatus.Neutral:
                    // ToDo: Allow bribing the pirates
                    break;
                case PirateBaseRelationStatus.Friendly:
                    _Sub.TeamID = CharacterTeamType.FriendlyNPC;
                    break;
                case PirateBaseRelationStatus.Hostile:
                default:
                    break;
            }
            


            Log.InternalDebug($"Spawned a pirate base with name {_Sub.Info.Name}");

            void SetupDE()
            {
                switch (CompatabilityHelper.Instance.BanditFaction.Reputation.Value)
                {
                    case >= +13:
                        Status = PirateBaseRelationStatus.Friendly;
                        break;
                    case <= -13:
                        Status = PirateBaseRelationStatus.Hostile;
                        break;
                    default:
                        Status = PirateBaseRelationStatus.Neutral;
                        break;
                }
                Log.Debug($"Base status: {Status}, rep: {CompatabilityHelper.Instance.BanditFaction.Reputation.Value}");
            }
        }

        StructureDamageTracker damageTracker;
        private bool threshholdCrossed;

        void SetupActive()
        {
            if (_Sub.GetItems(alsoFromConnectedSubs: false).Find(i => i.HasTag("reactor") && !i.NonInteractable)?.GetComponent<Reactor>() is Reactor reactor)
            {
                reactor.PowerUpImmediately();
                reactor.FuelConsumptionRate = 0; // never run out of fuel

                // Make sure the reactor doesn't explode lol
                if (CompatabilityHelper.Instance.HazardousReactorsInstalled)
                {
                    reactor.Item.InvulnerableToDamage = true;

                    // If we have a different reactor mod installed (e.g. immersive repairs) make the reactor not degrade
                } else if (CompatabilityHelper.Instance.ReactorModInstalled)
                {
                    Repairable repair = reactor.Item.GetComponent<Repairable>();
                    if (repair != null)
                    {
                        repair.DeteriorationSpeed = 0;
                        repair.MinDeteriorationDelay = float.PositiveInfinity;
                        repair.MinDeteriorationCondition = 100;
                    }
                }
            }

            if (Status != PirateBaseRelationStatus.Hostile)
            {
                damageTracker = new StructureDamageTracker(_Sub);
                damageTracker.ThresholdCrossed += DamageTracker_ThresholdCrossed;
                damageTracker.DamageAfterThreshold += DamageTracker_DamageAfterThreshold;
                threshholdCrossed = false;
            }
        }

        private void DamageTracker_DamageAfterThreshold(float amount)
        {

        }

        private void DamageTracker_ThresholdCrossed()
        {
            // Send message
            threshholdCrossed = true;
            Log.Debug("Threshold Crossed");
        }

        void SetupDestroyed()
        {
            _Sub.Info.Type = SubmarineType.Outpost;
            if (Main.IsClient) return;
            var baseItems = _Sub.GetItems(alsoFromConnectedSubs: false);
            if (baseItems.Find(i => i.HasTag("reactor") && !i.NonInteractable)?.GetComponent<Reactor>() is Reactor reactor)
            {
                reactor.AutoTemp = false;
                reactor.PowerOn = false;
            }

            var waypoints = _Sub.GetWaypoints(false).Where(wp => wp.SpawnType == SpawnType.Human || wp.SpawnType == SpawnType.Cargo);
            foreach (var wp in waypoints)
            {
                Level.Loaded.PositionsOfInterest.Add(new Level.InterestingPosition(wp.WorldPosition.ToPoint(), Level.PositionType.Wreck));
            }

            //break powered items
            foreach (Item item in baseItems.Where(it => it.Components.Any(c => c is Powered) && it.Components.Any(c => c is Repairable)))
            {
                if (item.NonInteractable || item.InvulnerableToDamage) { continue; }
                if (Rand.Range(0f, 1f, Rand.RandSync.Unsynced) < 0.8f)
                {
                    item.Condition *= Rand.Range(0f, 0.2f, Rand.RandSync.Unsynced);
                }
            }

            // min walls to damage
            var walls = Structure.WallList.Where(s => s.Submarine == _Sub);
            int wallCount = walls.Count();
            int damagedWallCount = Rand.Range(wallCount / 2, wallCount, Rand.RandSync.Unsynced);

            var avaliableWalls = walls.ToList();

            for (int i = 0; i < damagedWallCount; i++)
            {
                var targetWall = avaliableWalls.GetRandom(Rand.RandSync.Unsynced);
                _ = avaliableWalls.Remove(targetWall);
                int sectionsToDamage = Rand.Range(targetWall.SectionCount / 4, targetWall.SectionCount, Rand.RandSync.Unsynced);
                while (sectionsToDamage > 0)
                {
                    sectionsToDamage--;
                    targetWall.AddDamage(sectionsToDamage, Rand.Range(targetWall.MaxHealth * 0.75f, targetWall.MaxHealth, Rand.RandSync.Unsynced));
                }
            }
        }

        public void Populate()
        {
            if (_Data.Status == PirateOutpostStatus.Active)
            {
                SetupActive();
            }
            else
            {
                SetupDestroyed();
            }

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

                // Set hard cap on pirates to prevent lag when there's a lot of mods installed
                amountCreated = Math.Max(amountCreated, 8);

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

                    var team = Status == PirateBaseRelationStatus.Friendly ? CharacterTeamType.FriendlyNPC : CharacterTeamType.None;

                    Character spawnedCharacter = CharacterUtils.CreateHuman(character, characters, characterItems, _Sub, team, null);

                    if (CompatabilityHelper.Instance.DynamicEuropaInstalled) DESetup();

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

                        // Why are you stealing from your friends :(
                        if (Status == PirateBaseRelationStatus.Friendly)
                        {
                            item.AllowStealing = false;
                            item.SpawnedInCurrentOutpost = true;
                        }
                    }
                    currentSpawns++;

                    void DESetup()
                    {
                        spawnedCharacter.Faction = "bandits";
                    }
                }
            }

            if (Status == PirateBaseRelationStatus.Friendly)
            {
                foreach (var item in _Sub.GetItems(true))
                {
                    if (item.Container?.Prefab.AllowStealingContainedItems ?? false) continue;
                    item.AllowStealing = false;
                    item.SpawnedInCurrentOutpost = true;
                }
                _Sub.TeamID = CharacterTeamType.FriendlyNPC;
            }

            HuskOutpost();
        }

        internal void OnRoundEnd(LevelData levelData)
        {
            if (Main.IsClient)
            {
                Log.Debug("Was client");
                return;
            }
            if (_Sub == null) return;
#if CLIENT
            bool success = GameMain.GameSession.CrewManager!.GetCharacters().Any(c => !c.IsDead);
#else
                bool success =
                    GameMain.Server != null &&
                    GameMain.Server.ConnectedClients.Any(c => c.InGame && c.Character != null && !c.Character.IsDead);
#endif

            if (!success)
            {
                Log.Debug("Did not succeed");
                return;
            }
            if (_Revealed) levelData.MLC().PirateData.Revealed = true;

            if (levelData.MLC().PirateData.Status == PirateOutpostStatus.Destroyed)
            {
                Log.Debug("Base was destroyed");
                return;
            }
            try
            {
                // If more than half of the crew or the commander is dead / incapacited / arrested, the outpost is destroyed
                bool crewStatus = characters.Select(c => c.IsDead || c.Removed || c.IsIncapacitated || c.IsHandcuffed).Count() > characters.Count / 2;
                if (_Commander.IsDead || _Commander.Removed || _Commander.IsHandcuffed || crewStatus)
                {
                    levelData.MLC().PirateData.Status = PirateOutpostStatus.Destroyed;
                    Log.Debug("base destroyed");
                }
                else
                {
                    Log.Debug($"Base still active: {crewStatus} dead: {_Commander.IsDead} removed: {_Commander.Removed} handcuffed: {_Commander.IsHandcuffed}");
                }
                LocationConnection con = Level.Loaded.StartLocation.Connections.Where(c => c.OtherLocation(Level.Loaded.StartLocation) == Level.Loaded.EndLocation).First();
                PirateOutpostDirector.UpdateStatus(levelData.MLC().PirateData, con);
            } catch(Exception e) 
            {
                DebugConsole.ThrowError("Error in pirate outpost OnRoundEnd", e);
            }
        }

        private void HuskOutpost()
        {
            if (_Data.Status != PirateOutpostStatus.Husked) return;
            Log.InternalDebug("You've met with a terrible fate, haven't you?");
            if (!AfflictionHelper.TryGetAffliction("huskinfection", out AfflictionPrefab husk))
            {
                Log.Error("Couldn't get the husk affliction!!!");
                return;
            }

            foreach (Character character in characters)
            {
                var huskAffliction = new Affliction(husk, 200);
                character.CharacterHealth.ApplyAffliction(character.AnimController.MainLimb, huskAffliction);
                character.CharacterHealth.Update((float)Timing.Step);
                character.Kill(CauseOfDeathType.Affliction, huskAffliction);
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

    public enum PirateBaseRelationStatus
    {
        Hostile,
        Neutral,
        Friendly
    }
}

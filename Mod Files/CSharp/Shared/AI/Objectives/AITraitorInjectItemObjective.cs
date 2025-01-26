using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static Barotrauma.AIObjectiveIdle;

namespace MoreLevelContent.Shared.AI
{
    internal class AITraitorObjectiveInjectItem : AIObjective
    {
        public override Identifier Identifier { get; set; } = "traitorinject".ToIdentifier();
        // public override bool IsLoop { get => true; set => throw new NotImplementedException(); }
        public override bool CanBeCompleted => true;
        public AITraitorObjectiveInjectItem(Character character, AIObjectiveManager objectiveManager, float priorityModifier, Identifier option = default, ImmutableArray<Identifier> targetItems = default) : base(character, objectiveManager, priorityModifier, option)
        {
            targetItemIdentifier = targetItems.GetRandomUnsynced();

            // Pick our hated job
            hatedJob = JobPrefab.Prefabs.Where(p => !p.HiddenJob).GetRandomUnsynced().Identifier;
            Log.Debug($"Hated Job: {hatedJob}");
            character.IsEscorted = true; // lets them wander on the players sub

            // for testing, makes them walk around
            // like normal people
            if (!Main.IsRelase)
            {
                var idleObjective = objectiveManager.GetObjective<AIObjectiveIdle>();
                if (idleObjective != null)
                {
                    idleObjective.Behavior = AIObjectiveIdle.BehaviorType.Active;
                }
            }

            actCasualTimer = 5;//Rand.Range(60, 120, Rand.RandSync.Unsynced);
            ForceWalk = true;
            character.OnAttacked += OnAttacked;
        }
        private bool HasItem => targetItem != null && character.Inventory.Contains(targetItem);
        private bool IsActingCasual => actCasualTimer > 0;

        private readonly Identifier targetItemIdentifier;
        private readonly Identifier hatedJob;


        const string TraitorTeamChangeIdentifier = "traitor";
        const float CloseEnoughToInject = 100.0f;
        const float InjectDelay = 0.5f;

        private float actCasualTimer;
        private bool _hasDoneSusAction = false;
        private bool _victimWasUsingItemWhenPicked = false;
        private bool _injectedVictim = false;
        private float _injectTimer = InjectDelay;

        readonly List<Character> previousVictims = new List<Character>();
        AIObjectiveGetItem findItemTask;
        AIObjectiveGoTo gotoVictimTask;
        Item targetItem;
        Character victim;

        // Set the priority of this to be low if we're cuffed or we're still acting /casual/
        protected override float GetPriority()
        {
            Priority = IsActingCasual ? 0 : AIObjectiveManager.RunPriority - 0.5f;
            return Priority;
        }

        private void OnAttacked(Character attacker, AttackResult attackResult)
        {
            if (attacker == null) return;
            if (_hasDoneSusAction && attacker.IsOnPlayerTeam && (attackResult.Damage > 1))
            {
                // we're made! switch team and start attacking
                if (!character.HasTeamChange(TraitorTeamChangeIdentifier))
                {
                    _ = character.TryAddNewTeamChange(TraitorTeamChangeIdentifier, new ActiveTeamChange(CharacterTeamType.None, ActiveTeamChange.TeamChangePriorities.Willful));
                    var fight = objectiveManager.GetObjective<AIFightIntrudersAnySubObjective>();
                    fight.ForceHighestPriority = true;
                    objectiveManager.AddObjective(fight);
                    character.Speak(TextManager.Get($"dialog.{character.JobIdentifier}.found").Value);
                    Abandon = true;
                }
            }
        }

        public override void Update(float deltaTime)
        {
            if (character.Submarine == null) return;
            if (!character.Submarine.IsConnectedTo(Submarine.MainSub)) return;
            if (actCasualTimer > 0) actCasualTimer -= deltaTime;
            base.Update(deltaTime);
        }
        AIObjectiveEscapeHandcuffs _EscapeHandcuffsSubObjective;
        protected override void Act(float deltaTime)
        {
            // don't do anything if we're cuffed 
            if (character.LockHands)
            {
                // If we're cuffed, try to break out
                _ = TryAddSubObjective(ref _EscapeHandcuffsSubObjective, () => new AIObjectiveEscapeHandcuffs(character, objectiveManager));
                return;
            }

            // don't do anything if we're not in the main sub and not docked to it
            if (!character.Submarine.IsConnectedTo(Submarine.MainSub)) return;

            // We're on the submarine, lets act casual for awhile
            if (IsActingCasual) return;

            // Time to spring into action
            if (!HasItem)
            {
                // We don't have the target item yet, lets try to find it
                FindTargetItem();
                return;
            }

            // We've found our target item, lets find a target to inject it into
            if (victim == null || victim.IsDead || victim.Removed)
            {
                victim = FindVictim();
                previousVictims.Add(victim);
                _victimWasUsingItemWhenPicked = victim.SelectedItem != null;
                DebugSpeak($"Picked new victim: {victim.Name}");

                _ = TryAddSubObjective(ref gotoVictimTask, () => 
                new AIObjectiveGoTo(victim, character, objectiveManager, closeEnough: CloseEnoughToInject)
                {
                    ForceWalk = true
                }, 
                () => GotToVictim(),
                () => CouldntGetToVictim());
            }

            // Wait until we finish all the sub-objectives before doing anything
            if (subObjectives.Any()) return;

            if (!character.CanInteractWith(victim))
            {
                // Go to the victim and select it
                RemoveSubObjective(ref gotoVictimTask);
                _ = TryAddSubObjective(ref gotoVictimTask, () => new AIObjectiveGoTo(victim, character, objectiveManager, closeEnough: CloseEnoughToInject)
                {
                    ForceWalk = true
                },
                onCompleted: () => GotToVictim(),
                onAbandon: () => CouldntGetToVictim()
                );
                return;
            }

            // We're at the target, time to posion them!
            if (!_injectedVictim) InjectItem(deltaTime);
        }

        // Leave the sceen of the crime
        private void Scram()
        {
            character.DeselectCharacter();
            Hull targetHull = GetEscapeHull();
            _ = TryAddSubObjective(ref gotoVictimTask, () =>
            {
                DebugSpeak("Getting outta dodge!");
                return new AIObjectiveGoTo(targetHull, character, objectiveManager)
                {
                    ForceWalk = true
                };
            },
            onCompleted: () =>
            {
                float time = Rand.Range(60, 120, Rand.RandSync.Unsynced);
                actCasualTimer = time;
                DebugSpeak($"Time to act casual for {time}");
                _victimWasUsingItemWhenPicked = false;
                _injectedVictim = false;
                _injectTimer = InjectDelay;
                victim = null;
                targetItem = null;
                RemoveSubObjective(ref findItemTask);
                RemoveSubObjective(ref gotoVictimTask);
            }
            );
        }

        private Hull GetEscapeHull()
        {
            List<Hull> potentialEscapeHulls = new List<Hull>();
            List<float> hullWeights = new List<float>();
            if (character.Submarine == null) return null;
            foreach (var hull in character.Submarine.GetHulls(true))
            {
                // taken form AIObjectiveIdle
                if (hull == null || hull.AvoidStaying || hull.IsWetRoom) { continue; }
                // Ignore very narrow hulls.
                if (hull.RectWidth < 200) { continue; }
                // Ignore hulls that are too low to stand inside.
                if (character.AnimController is HumanoidAnimController animController)
                {
                    if (hull.CeilingHeight < ConvertUnits.ToDisplayUnits(animController.HeadPosition.Value))
                    {
                        continue;
                    }
                }

                if (!potentialEscapeHulls.Contains(hull))
                {
                    float weight = hull.RectWidth;

                    // prefer distant hulls
                    float yDist = Math.Abs(character.WorldPosition.Y - hull.WorldPosition.Y);
                    yDist = yDist > 100 ? yDist * 5 : 0;
                    float dist = Math.Abs(character.WorldPosition.X - hull.WorldPosition.X) + yDist;
                    float distanceFactor = MathHelper.Lerp(1, 0, MathUtils.InverseLerp(2500, 0, dist));

                    // prefer hulls with less water
                    float waterFactor = MathHelper.Lerp(1, 0, MathUtils.InverseLerp(0, 100, hull.WaterPercentage * 2));
                    weight *= distanceFactor * waterFactor;

                    potentialEscapeHulls.Add(hull);
                    hullWeights.Add(weight);
                }
            }

            return !potentialEscapeHulls.Any() ? null : ToolBox.SelectWeightedRandom(potentialEscapeHulls, hullWeights, Rand.RandSync.Unsynced);
        }

        private void InjectItem(float deltaTime)
        {
            SteeringManager.Reset();

            if (character.SelectedCharacter != victim)
            {
                character.SelectCharacter(victim);
            }

            if (_injectTimer > 0.0f)
            {
                _injectTimer -= deltaTime;
                return;
            }
            _injectTimer = InjectDelay;

            targetItem.ApplyTreatment(character, victim, victim.AnimController.MainLimb);
            _injectedVictim = true;
            DebugSpeak("Injected the item!");

            // We did it, time to get outta here!
            Scram();
        }

        private void GotToVictim()
        {
            // We got to them
            RemoveSubObjective(ref gotoVictimTask);

            // If they were using an item when we picked them, check if they're still using it
            if (_victimWasUsingItemWhenPicked && victim.SelectedItem == null)
            {
                // They're not using it, abort
                _victimWasUsingItemWhenPicked = false;
                victim = null;
                DebugSpeak($"They're not using the item anymore, abort!");
                return;
            }
        }

        private void CouldntGetToVictim()
        {
            // We couldn't get to them
            RemoveSubObjective(ref gotoVictimTask);
            DebugSpeak($"Couldn't get to victim: {victim.Name}! Picking a new one...");
            _victimWasUsingItemWhenPicked = false;
            victim = null;
            return;
        }

        private void FindTargetItem()
        {
            if (targetItem == null)
            {
                _ = TryAddSubObjective(ref findItemTask, () =>
                {
                    DebugSpeak("Going to find the poison :)");
                    return new AIObjectiveGetItem(character, targetItemIdentifier, objectiveManager, spawnItemIfNotFound: false, checkInventory: true)
                    {
                        ForceWalk = true,
                        AllowStealing = true,
                        SpeakIfFails = false
                    };
                }, FoundItem, FailedToFindItem);

                void FoundItem()
                {
                    RemoveSubObjective(ref findItemTask);
                    _hasDoneSusAction = true;
                    TrySetTargetItem(character.Inventory.FindItemByIdentifier(targetItemIdentifier, true));
                    DebugSpeak("Found the poison");
                    actCasualTimer = 5; // act casual for a bit
                }

                void FailedToFindItem()
                {
                    // Wait for a bit before trying to find it again
                    actCasualTimer = 10;

                    // Try to spawn the item in a traitor pannel
                    ItemPrefab prefab = FindItemPrefab(targetItemIdentifier);
                    if (!TryFindSuitableContainer(out Item container))
                    {
                        // We couldn't find a spot to spawn the item, just spawn it in our inventory
                        Entity.Spawner.AddItemToSpawnQueue(prefab, character.Inventory);
                        DebugSpeak("Couldn't find a place to spawn it, spawning it in my inventory!");
                        return;
                    }

                    // We found a spot to spawn the item in, lets spawn it there
                    Entity.Spawner.AddItemToSpawnQueue(prefab, container.OwnInventory);
                    DebugSpeak("Spawned the item in a hidden container!");
                }
            }
        }

        protected ItemPrefab FindItemPrefab(Identifier identifier) => (ItemPrefab)MapEntityPrefab.List.FirstOrDefault(prefab => prefab is ItemPrefab && prefab.Identifier == identifier);

        private void TrySetTargetItem(Item item)
        {
            if (targetItem == item)
            {
                Log.Debug("Failed to get item!");
                return;
            }
            targetItem = item;
        }

        private Character FindVictim()
        {
            return Character.CharacterList.Where(c => Filter(c)).OrderByDescending(c => CheckPriority(c)).First();

            bool Filter(Character c) =>
                !c.Removed &&
                !c.IsDead &&
                c.IsHuman &&
                c.IsOnPlayerTeam &&
                c.Submarine != null &&
                c.Submarine.IsConnectedTo(Submarine.MainSub);

            int CheckPriority(Character potentialVictim)
            {
                int priority = 0;

                // Things that increase priority
                if (potentialVictim.IsPlayer) priority += 2;
                if (potentialVictim.JobIdentifier == hatedJob) priority += 2;
                if (potentialVictim.SelectedItem != null)
                {
                    if (potentialVictim.SelectedItem.Tags.Contains("turret")) priority += 3;
                    if (potentialVictim.SelectedItem.Tags.Contains("navterminal")) priority += 2;
                    if (potentialVictim.SelectedItem.Tags.Contains("fabricator")) priority++;
                }

                // Things that decrease priority
                if (potentialVictim.SelectedItem == null) priority--;
                if (potentialVictim.IsUnconscious) priority--;
                if (potentialVictim.IsIncapacitated) priority--;
                if (HumanAIController.GetHullSafety(potentialVictim.CurrentHull, potentialVictim) > HumanAIController.HULL_SAFETY_THRESHOLD) priority -= 4;
                if (previousVictims.Contains(potentialVictim)) priority -= 4;

                if (potentialVictim.IsBot)
                {
                    // Reduce priority of bots more if we're in multiplayer
                    if (GameMain.IsMultiplayer && potentialVictim.IsBot) priority = 0;
                    else priority--;
                }
                return priority;
            }
        }

        private bool TryFindSuitableContainer(out Item container)
        {
            List<Item> suitableItems = new List<Item>();
            List<Identifier> allowedContainerIdentifiers = new List<Identifier>()
            {
                "loosevent",
                "loosepanel"
            };
            foreach (Item item in Item.ItemList)
            {
                if (item.HiddenInGame || item.NonInteractable || item.NonPlayerTeamInteractable) { continue; }
                if (item.Submarine == null || !item.Submarine.IsConnectedTo(character.Submarine))
                {
                    continue;
                }
                if (item.GetComponent<ItemContainer>() != null && allowedContainerIdentifiers.Contains(((MapEntity)item).Prefab.Identifier))
                {
                    if ((!item.OwnInventory.IsFull()))
                    {
                        suitableItems.Add(item);
                    }
                }
            }
            container = suitableItems.GetRandomUnsynced();
            return container != null;
        }

        protected void DebugSpeak(string msg)
        {
            if (!Main.IsRelase)
            {
                character.Speak(msg);
            }
        }

        // never abort
        //protected bool CheckObjectiveSpecific() => false;
        protected override bool CheckObjectiveState() => throw new NotImplementedException();
    }
}

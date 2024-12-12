using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Utils;
using static Barotrauma.Level;

namespace MoreLevelContent.Missions
{
    // Shared
    partial class DistressEscortMission : DistressMission
    {
        private readonly XElement characterConfig;
        private readonly LocalizedString sonarLabel;
        private readonly PositionType spawnPositionType;
        private readonly bool hostile;

        private readonly MissionNPCCollection missionNPCs;


        private int calculatedReward;
        private Submarine missionSub;
        private Vector2 spawnPosition;

        public override IEnumerable<(LocalizedString Label, Vector2 Position)> SonarLabels
        {
            get
            {
                if (State != 1)
                yield return (Prefab.SonarLabel.IsNullOrEmpty() ? sonarLabel : Prefab.SonarLabel, spawnPosition);
            }
        }

        public DistressEscortMission(MissionPrefab prefab, Location[] locations, Submarine sub)
            : base(prefab, locations, sub)
        {
            missionSub = sub;
            characterConfig = prefab.ConfigElement.GetChildElement("Characters");
            sonarLabel = TextManager.Get("missionname.distressmission");
            spawnPositionType = characterConfig.GetAttributeEnum("spawntype", PositionType.Cave);
            hostile = characterConfig.GetAttributeBool("hostile", false);
            missionNPCs = new(this, characterConfig);
            CalculateReward();
        }

        private void CalculateReward()
        {
            if (missionSub == null)
            {
                calculatedReward = Prefab.Reward;
                return;
            }

            // Disabled for now, because they make balancing the missions a pain.
            int multiplier = 1;//CalculateScalingEscortedCharacterCount();
            calculatedReward = Prefab.Reward * multiplier;

            string rewardText = $"‖color:gui.orange‖{string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:N0}", GetReward(missionSub))}‖end‖";
            if (descriptionWithoutReward != null) { description = descriptionWithoutReward.Replace("[reward]", rewardText); }
        }

        public override float GetBaseReward(Submarine sub)
        {
            if (sub != missionSub)
            {
                missionSub = sub;
                CalculateReward();
            }
            return calculatedReward;
        }

        private void InitEscort()
        {
            missionNPCs.Clear();

            if (!Loaded.TryGetInterestingPosition(false, spawnPositionType, 0, out InterestingPosition _spawnPos))
            {
                Log.Error($"Failed to find a spawn position of type {spawnPositionType}! Falling back to any position.");
                bool failed = Loaded.TryGetInterestingPosition(false, PositionType.MainPath | PositionType.SidePath | PositionType.Cave | PositionType.Wreck, 0, out _spawnPos);
                if (failed)
                {
                    Log.Error("Could not find ANY position to spawn distress humans at!");
                    spawnPosition = Vector2.Zero;
                }
            }
            spawnPosition = _spawnPos.Position.ToVector2();

            CharacterTeamType team = hostile ? CharacterTeamType.None : CharacterTeamType.FriendlyNPC;
            missionNPCs.CreateHumansAtPosition(team, spawnPosition, OnCharacterCreated);

            void OnCharacterCreated(Character character, XElement characterMissionConfig)
            {
                character.MLC().NPCElement = characterMissionConfig;
                if (character.AIController is not HumanAIController humanAI) return;

                // Only turn on the mental state for non-hostile divers
                // so that sometimes you'll get insane divers that attack you
                if (!hostile) humanAI.InitMentalStateManager();

                // Force the AI to wait in the location so if they get spooked by damage they
                // don't try to swim cross map to your sub to find saftey
                var waitOrder = OrderPrefab.Prefabs["wait"].CreateInstance(OrderPrefab.OrderTargetType.Entity);
                humanAI.SetForcedOrder(waitOrder);

                int minMoney = characterMissionConfig.GetAttributeInt("minmoney", 0);
                int maxMoney = characterMissionConfig.GetAttributeInt("maxmoney", 0);

                if (maxMoney > 0)
                {
                    int money = Rand.Range(minMoney, maxMoney, Rand.RandSync.Unsynced);
                    character.Wallet.Give(money);
                    Log.InternalDebug($"Gave {money} to {character.Name}");
                }

                if (characterMissionConfig.GetAttributeBool("allowordering", false))
                {
#if CLIENT
                    GameMain.GameSession.CrewManager.AddCharacterToCrewList(character);
#endif
                }
            }
        }

        private void InitCharacters()
        {
            spawnPosition = missionNPCs[0].WorldPosition;
        }

        protected override void StartMissionSpecific(Level level)
        {
            if (missionNPCs.characters.Count > 0)
            {
#if DEBUG
                throw new Exception($"characters.Count > 0 ({missionNPCs.characters.Count})");
#else
                DebugConsole.AddWarning("Character list was not empty at the start of a escort mission. The mission instance may not have been ended correctly on previous rounds.");
                missionNPCs.characters.Clear();         
#endif
            }

            if (characterConfig == null)
            {
                DebugConsole.ThrowError("Failed to initialize characters for escort mission (characterConfig == null)");
                return;
            }

            // to ensure single missions run without issues, default to mainsub
            if (missionSub == null)
            {
                missionSub = Submarine.MainSub;
                CalculateReward();
            }

            if (!IsClient)
            {
                InitEscort();
                InitCharacters();
            }
        }

        const float MINDIST = 2000f;
        bool triggered = false;

        protected override void UpdateMissionSpecific(float deltaTime)
        {
            if (IsClient) return;
            // Exit if we're client or if we're already active or if all of the characters are dead

            if (missionNPCs.characters.Any(c => !MissionNPCCollection.IsAlive(c)))
            {
                State = 1;
            }

            if (triggered || missionNPCs.characters.Any(c => !MissionNPCCollection.IsAlive(c))) return;

            foreach (var aiCharacter in missionNPCs.characters.Where(c => MissionNPCCollection.IsAlive(c)))
            {
                if (!triggered && ShouldActivate())
                {
                    Activate();
                    return;
                }

                bool ShouldActivate()
                {
                    bool shouldActivate = GameSession.GetSessionCrewCharacters(CharacterType.Player).Any(c => MissionNPCCollection.Close(c, aiCharacter, MINDIST));
                    return shouldActivate;
                }
            }
        }

        /// <summary>
        /// Removes the forced wait order from the AI
        /// and forces them into combat with the closest player
        /// if they're hostile
        /// </summary>
        private void Activate()
        {
            triggered = true;
            Log.Debug("Activated NPC");
            foreach (var aiCharacter in missionNPCs.characters)
            {
                if (aiCharacter.AIController is HumanAIController humanAI)
                {
                    humanAI.ClearForcedOrder();
                    if (hostile)
                    {
                        Character target = MissionNPCCollection.GetClosest(aiCharacter);
                        if (target != null) humanAI.AddCombatObjective(AIObjectiveCombat.CombatMode.Offensive, target);
                    }
                }
            }
        }


        protected override bool DetermineCompleted()
        {
            if (Submarine.MainSub != null && Submarine.MainSub.AtEndExit)
            {
                if (hostile)
                {
                    return triggered;
                }
                return missionNPCs.characters.All(c => MissionNPCCollection.Survived(c));
            }
            return false;
        }

        protected override void EndMissionSpecific(bool completed)
        {
            if (!IsClient)
            {
                missionNPCs.End(completed);
            }
            missionNPCs.Clear();
            base.EndMissionSpecific(completed);
        }
    }
}

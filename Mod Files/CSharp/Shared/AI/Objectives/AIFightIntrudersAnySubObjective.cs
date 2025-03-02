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
    // Targets list not populating is probably the issue
    internal class AIFightIntrudersAnySubObjective : AIObjectiveFightIntruders
    {
        public AIFightIntrudersAnySubObjective(Character character, AIObjectiveManager objectiveManager, float priorityModifier = 1) : base(character, objectiveManager, priorityModifier)
        {
        }

        protected override bool AllowInAnySub => true;


        protected override void FindTargets()
        {
            foreach (Character target in GetList())
            {
                if (!IsValidTarget(target)) { continue; }
                if (!character.CanSeeTarget(target)) { continue; }
                if (!ignoreList.Contains(target))
                {
                    Targets.Add(target);
                    if (Targets.Count > MaxTargets)
                    {
                        break;
                    }
                }
            }
        }


    }
}

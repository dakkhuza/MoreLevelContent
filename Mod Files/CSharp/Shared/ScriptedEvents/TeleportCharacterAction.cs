using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using System.Collections.Generic;
using System.Linq;
using static Barotrauma.Level;

namespace MoreLevelContent
{
    internal class TeleportCharacterAction : EventAction
    {
        [Serialize("", IsPropertySaveable.Yes, description: "Tag of the target(s) to teleport.")]
        public Identifier TargetTag { get; set; }

        public TeleportCharacterAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
        }

        private bool isFinished;

        public override void Update(float deltaTime)
        {
            if (isFinished) { return; }
            // Try to find a ruin to tp to
            Submarine wreck = null;
            Submarine ruin = null;
            foreach (var sub in Submarine.Loaded)
            {
                if (sub.Info.Type == SubmarineType.Ruin)
                {
                    ruin = sub;
                    break;
                }
                if (sub.Info.Type == SubmarineType.Wreck)
                {
                    wreck = sub;
                }
            }

            try
            {
                Tp();
            } catch { Teleport(Vector2.Zero); }
            isFinished = true;

            void Tp()
            {
                if (ruin != null)
                {
                    Teleport(ruin);
                    return;
                }
                // If we can't find a ruin, try to find a wreck
                if (wreck != null)
                {
                    Teleport(wreck);
                    return;
                }
                // If both those fail, try to find an abyss cave
                if (Loaded != null && Loaded.TryGetInterestingPosition(false, PositionType.AbyssCave, Sonar.DefaultSonarRange, out InterestingPosition pos, suppressWarning: true))
                {
                    Teleport(pos.Position.ToVector2());
                    return;
                }
                // Try to find a cave, main path or side path position
                if (Loaded != null && Loaded.TryGetInterestingPosition(false, PositionType.Cave | PositionType.MainPath | PositionType.SidePath, Sonar.DefaultSonarRange, out InterestingPosition pos2, suppressWarning: true))
                {
                    Teleport(pos2.Position.ToVector2());
                    return;
                }
                // If all else fails, teleport to a random waypoint or 0,0
                Teleport(WayPoint.GetRandom(SpawnType.Path)?.WorldPosition ?? Vector2.Zero);
            }
        }

        void Teleport(Submarine target)
        {
            var wp = WayPoint.GetRandom(sub: target);
            Teleport(wp.WorldPosition);
        }

        void Teleport(Vector2 worldPos)
        {
            foreach (var target in ParentEvent.GetTargets(TargetTag))
            {
                if (target is Character c)
                {
                    c.TeleportTo(worldPos);
                }
            }
        }

        public override bool IsFinished(ref string goToLabel)
        {
            return isFinished;
        }

        public override void Reset() { isFinished = false; }
    }
}

using Barotrauma;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Linq;

namespace MoreLevelContent.Missions
{
    // Shared
    internal partial class TriangulationMission : Mission
    {
        private readonly LocalizedString sonarLabel0;
        private readonly LocalizedString sonarLabel1;
        private readonly LocalizedString sonarLabel2;
        private LevelData levelData;

        public TriangulationMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            sonarLabel0 = TextManager.Get("tri-point-1");
            sonarLabel1 = TextManager.Get("tri-point-1");
            sonarLabel2 = TextManager.Get("tri-point-1");

            LevelData levelData = locations[0].Connections.Where(c => c.Locations.Contains(locations[1])).FirstOrDefault()?.LevelData ?? locations[0]?.LevelData;
            if (levelData != null)
            {
                SetLevel(levelData);
            }
        }

        protected override bool DetermineCompleted() => false;


        public override IEnumerable<(LocalizedString Label, Vector2 Position)> SonarLabels => base.SonarLabels;

        public override void SetLevel(LevelData level)
        {
            if (levelData != null)
            {
                //level already set
                return;
            }

            levelData = level;

            switch (level.MLC().TriangulationTarget)
            {
                case TriangulationTarget.None:
                    break;
                case TriangulationTarget.MapFeature:
                    break;
                case TriangulationTarget.PirateBase:
                    break;
                case TriangulationTarget.Treasure:
                    break;
                default:
                    break;
            }
        }
    }
}

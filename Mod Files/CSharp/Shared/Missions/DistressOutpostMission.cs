using Barotrauma;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Generation;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static Barotrauma.Level;

namespace MoreLevelContent.Missions
{
    // Shared
    partial class DistressOutpostMission : DistressMission
    {
        private readonly XElement decoConfig;


        private LevelData levelData;

        public DistressOutpostMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            // Setup deco
            decoConfig = prefab.ConfigElement.GetChildElement("DecoItems");

            // for campaign missions, set level at construction
            LevelData levelData = locations[0].Connections.Where(c => c.Locations.Contains(locations[1])).FirstOrDefault()?.LevelData ?? locations[0]?.LevelData;
            if (levelData != null)
            {
                SetLevel(levelData);
            }
        }

        public override void SetLevel(LevelData level)
        {
            if (levelData != null)
            {
                //level already set
                return;
            }

            levelData = level;
            List<ContentFile> decos = new();
            
            foreach (var item in decoConfig.Elements())
            {
                ContentPath path = item.GetAttributeContentPath("path", Prefab.ContentPackage);
                ContentFile deco = ContentPackageManager.EnabledPackages.All.SelectMany(p => p.GetFiles<OutpostModuleFile>()).Where(f => f.Path.Value == path).FirstOrDefault();
                if (deco == null)
                {
                    Log.Error($"No outpost module found with path {path}, skipping...");
                    continue;
                }
                decos.Add(deco);
                Log.Debug("Added item to deco");
            }

            MissionGenerationDirector.RequestDecorate(decos, OnDecoCreated, false);
        }

        void OnDecoCreated(List<Submarine> decoItems, Cave decoratedCave)
        {
            Log.Debug($"Decorated cave with {decoItems.Count}");
            decoratedCave.DisplayOnSonar = true;
        }

        protected override bool DetermineCompleted() => false;
    }
}

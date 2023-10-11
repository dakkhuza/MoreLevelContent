using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Barotrauma;
using Barotrauma.Extensions;
using MoreLevelContent.Shared.Generation;
using static Barotrauma.Level;
using static HarmonyLib.Code;

namespace MoreLevelContent.Shared.Store
{
    public class PirateStore : StoreBase<PirateStore>
    {
        private List<PirateOutpostDef> pirateOutposts;
        private List<PirateNPCSetDef> pirateSets;

        public override void Setup()
        {
            pirateOutposts = new List<PirateOutpostDef>();
            pirateSets = new List<PirateNPCSetDef>();
            HasContent = FindAndScoreOutpostFiles() && FindAndScoreNPCs();
        }

        public PirateNPCSetDef GetNPCSetForDiff(float diff) => GetElementWithPreferedDifficulty(diff, pirateSets);

        internal PirateOutpostDef GetPirateOutpostForDiff(float diff) => GetElementWithPreferedDifficulty(diff, pirateOutposts);
        private bool FindAndScoreOutpostFiles()
        {
            Log.Debug("Collecting pirate outposts...");

            foreach (var missionPrefab in MissionPrefab.Prefabs)
            {
                if (missionPrefab.Tags.Contains("piratebase") && missionPrefab.Tags.Contains("mlc"))
                {
                    ContentXElement element = missionPrefab.ConfigElement;
                    foreach (var item in element.GetChildElements("PirateBase"))
                    {
                        ContentPath path = item.GetAttributeContentPath("path");
                        float minDif = item.GetAttributeFloat("min", 0);
                        float maxDif = item.GetAttributeFloat("max", 100);
                        PlacementType placement = item.GetAttributeEnum("placement", PlacementType.Bottom);

                        SubmarineInfo subInfo = new SubmarineInfo(path.Value);
                        SubmarineFile file = ContentPackageManager.EnabledPackages.All.SelectMany(p => p.GetFiles<SubmarineFile>()).Where(f => f.Path.Value == path).FirstOrDefault();
                        DifficultyRange range = new DifficultyRange(minDif, maxDif);
                        pirateOutposts.Add(new PirateOutpostDef(file, range, placement));
                        Log.Debug($"Added pirate outpost with path {path} and range {range}");
                    }
                }
            }

            Log.Debug("Sorting modules by their diff ranges...");
            pirateOutposts.Sort();

            foreach (var item in pirateOutposts)
            {
                Log.Verbose(item.DifficultyRange.ToString());
            }

            if (pirateOutposts.Count > 0)
            {
                Log.Debug($"Collected {pirateOutposts.Count} pirate outposts");
                return true;
            }
            else
            {
                Log.Error("Failed to find any pirate outposts!!!");
                return false;
            }
        }

        private bool FindAndScoreNPCs()
        {
            List<MissionPrefab> pirateMissions = MissionPrefab.Prefabs.Where(m => m.Identifier.StartsWith("mlc_mp")).ToList();

            foreach (MissionPrefab prefab in pirateMissions)
            {
                pirateSets.Add(new PirateNPCSetDef(prefab, prefab.Name.Value));
            }

            Log.Verbose("Sorting sets by their diff ranges...");
            pirateSets.Sort();

            if (pirateSets.Count == 0)
            {
                Log.Error("Failed to find pirates to spawn :(");
                return false;
            }
            Log.Verbose($"Collected {pirateSets.Count} pirate NPC sets to choose from.");
            return true;
        }

    }
}

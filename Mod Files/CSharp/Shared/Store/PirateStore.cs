using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Barotrauma;
using Barotrauma.Extensions;
using MoreLevelContent.Shared.Generation;
using static Barotrauma.Level;

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
        internal PirateOutpostDef FindOutpostWithPath(string path)
        {
            return pirateOutposts.Find(p => p.SubInfo.FilePath == path);
        }
        internal void DumpPirateOutposts()
        {
            if (pirateOutposts.Count == 0)
            {
                Log.Warn("No pirate outposts found!");
                return; 
            }
            foreach (var item in pirateOutposts)
            {
                Log.Debug(item.SubInfo.FilePath);
            }
        }

        public PirateNPCSetDef GetNPCSetForDiff(float diff) => GetElementWithPreferedDifficulty(diff, pirateSets);

        internal PirateOutpostDef GetPirateOutpostForDiff(float diff) => GetElementWithPreferedDifficulty(diff, pirateOutposts);

        private bool FindAndScoreOutpostFiles()
        {
            Log.Debug("Collecting pirate outposts...");
            var pirateOutpostSets = MissionPrefab.Prefabs.Where(m => m.Tags.Contains("pirateoutpostset"));
            Log.Debug($"outposts: {pirateOutpostSets.Count()}");
            foreach (var item in pirateOutpostSets)
            {
                foreach (var outpost in item.ConfigElement.GetChildElements("PirateOutpost"))
                {
                    var path = outpost.GetAttributeContentPath("path");
                    var min = outpost.GetAttributeInt("mindiff", 0);
                    var max = outpost.GetAttributeInt("maxdiff", 100);
                    var placement = outpost.GetAttributeEnum("placement", PlacementType.Bottom);
                    SubmarineInfo subInfo = new SubmarineInfo(path.Value);
                    pirateOutposts.Add(new PirateOutpostDef(subInfo, min, max, placement));
                }
            }

            pirateOutposts = pirateOutposts.OrderBy(o => o.SubInfo.Name).ToList();

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

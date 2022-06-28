using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Barotrauma;
using Barotrauma.Extensions;
using MoreLevelContent.Shared.Generation;

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

        public PirateNPCSetDef GetNPCSetForDiff(float diff) => GetElementsForDiff(diff, pirateSets).GetRandom(Rand.RandSync.ServerAndClient);

        internal PirateOutpostDef GetPirateOutpostForDiff(float diff) => GetElementsForDiff(diff, pirateOutposts).GetRandom(Rand.RandSync.ServerAndClient);

        private bool FindAndScoreOutpostFiles()
        {
            Log.Debug("Collecting pirate outposts...");
            var outpostModuleFiles = ContentPackageManager.EnabledPackages.All
            .SelectMany(p => p.GetFiles<OutpostModuleFile>())
            .OrderBy(f => f.UintIdentifier).ToList();

            foreach (var outpostModuleFile in outpostModuleFiles)
            {
                SubmarineInfo subInfo = new SubmarineInfo(outpostModuleFile.Path.Value);
                if (subInfo.OutpostModuleInfo != null)
                {
                    if (subInfo.OutpostModuleInfo.AllowedLocationTypes.Contains("ilo_PirateOutpost"))
                        pirateOutposts.Add(new PirateOutpostDef(outpostModuleFile, subInfo));
                }
            }

            Log.Debug("Sorting modules by their diff ranges...");
            pirateOutposts.Sort();

            foreach (var item in pirateOutposts)
            {
                Log.Debug(item.DifficultyRange.ToString());
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

            Log.Debug("Sorting sets by their diff ranges...");
            pirateSets.Sort();

            if (pirateSets.Count == 0)
            {
                Log.Error("Failed to find pirates to spawn :(");
                return false;
            }
            Log.Debug($"Collected {pirateSets.Count} pirate NPC sets to choose from.");
            return true;
        }

    }
}

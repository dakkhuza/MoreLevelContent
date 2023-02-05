using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Generation.Pirate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoreLevelContent.Shared.Store
{ 
    public abstract class StoreBase<T> : Singleton<T> where T : class
    {
        public static bool HasContent { get; protected set; }

        protected Element GetElementWithPreferedDifficulty<Element>(float preferedDifficulty, List<Element> elements, float maxDifference = 20f) where Element : DefWithDifficultyRange
        {
            Log.InternalDebug($"Looking for {typeof(Element).Name} with perfered difficulty of {preferedDifficulty}...");
            List<Element> filtered = elements;
            try
            {
                filtered = elements.Where(e => MathF.Abs(e.AverageDifficulty - preferedDifficulty) < maxDifference).ToList();
            } catch(Exception e)
            {
                Log.Error(e.ToString());
            }

            if (filtered.Count == 0)
            {
                Log.Warn($"Failed to find element of type '{nameof(Element)}' with prefered difficulty of {preferedDifficulty} with a max differential of {maxDifference}!");
                filtered = elements;
            }

            Log.Verbose($"Filtered sets of '{nameof(Element)}' to choose from {filtered.Count}");

            filtered = filtered.OrderBy(e => e.AverageDifficulty).ToList();
            Element selectedElement = ToolBox.SelectWeightedRandom(filtered, (elm) =>
            {
                return elm.AverageDifficulty > preferedDifficulty
                    ? preferedDifficulty / elm.AverageDifficulty
                    : elm.AverageDifficulty / preferedDifficulty;
            }, Rand.RandSync.ServerAndClient);

            return selectedElement;
        }

        internal List<OutpostModuleFile> GetOutpostModuleFilesWithLocation(string locationType)
        {
            var outpostModuleFiles = ContentPackageManager.EnabledPackages.All
                .SelectMany(p => p.GetFiles<OutpostModuleFile>())
                .OrderBy(f => f.UintIdentifier).ToList();
            List<OutpostModuleFile> modulesWithTag = new();
            foreach (var outpostModuleFile in outpostModuleFiles)
            {
                SubmarineInfo subInfo = new SubmarineInfo(outpostModuleFile.Path.Value);
                if (subInfo.OutpostModuleInfo != null)
                {
                    if (subInfo.OutpostModuleInfo.AllowedLocationTypes.Contains(locationType))
                        modulesWithTag.Add(outpostModuleFile);
                }
            }
            return modulesWithTag;
        }
    }
}

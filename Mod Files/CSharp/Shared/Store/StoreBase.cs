using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoreLevelContent.Shared.Store
{ 
    public abstract class StoreBase<T> : Singleton<T> where T : class
    {
        public static bool HasContent { get; protected set; }

        protected List<Deff> GetElementsForDiff<Deff>(float diff, List<Deff> elements) where Deff : DefWithDifficultyRange
        {
            Log.InternalDebug($"Looking for {typeof(Deff).Name} with difficulty of {diff}...");

            List<Deff> suitableDefs = elements.Where(d => d.DifficultyRange.IsInRangeOf(diff)).ToList();
            if (suitableDefs.Count == 0)
            {
                Log.InternalDebug($"No defs found for difficulty {diff}! Falling back to the highest level difficulty...");
                suitableDefs.Add(elements.First());
            }

            if (suitableDefs.Count == 0)
            {
                Log.Error("DEFS WAS ZERO, SOMETHING WENT __HORRIBLY__ WRONG!");
            }

            Log.InternalDebug($"Found {suitableDefs.Count} suitable {typeof(T).Name} to choose from.");
            suitableDefs.Sort();
            return suitableDefs;
        }

    }
}

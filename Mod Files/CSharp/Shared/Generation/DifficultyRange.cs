using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MoreLevelContent.Shared.Generation
{
    public struct DifficultyRange
    {
        public float MinDiff;
        public float MaxDiff;

        private static readonly Regex diffRegex = new Regex("diff_([0-9.]+)-([0-9.]+)");

        public DifficultyRange(string name)
        {
            Match match = diffRegex.Match(name);
            // Exit if the sub has no difficulty range defined
            if (match.Groups.Count < 2)
            {
                MinDiff = 0;
                MaxDiff = 0;
                Log.Debug($"Element with name {name} has no diff range defined. Will only spawn when at 0% diff!");
                return;
            }
            string diffStr1 = match.Groups[1].Value;
            string diffStr2 = match.Groups[2].Value;

            Log.Verbose($"Diff 1: {diffStr1}");
            Log.Verbose($"Diff 2: {diffStr2}");

            MinDiff = float.Parse(diffStr1);
            MaxDiff = float.Parse(diffStr2);
            Log.Debug($"Parsed diff for element with name {name} as {MinDiff}-{MaxDiff}%");
        }

        public override string ToString() => $"{MinDiff} - {MaxDiff}";

        public bool IsInRangeOf(float diff) => MinDiff <= diff && diff < MaxDiff;
    }
}

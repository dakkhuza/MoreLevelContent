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

        public DifficultyRange(float min, float max)
        {
            MinDiff = min;
            MaxDiff = max;
        }

        public DifficultyRange(string name)
        {
            Match match = diffRegex.Match(name);
            // Exit if the sub has no difficulty range defined
            if (match.Groups.Count < 2)
            {
                MinDiff = 0;
                MaxDiff = 0;
                Log.Warn($"Element with name {name} has no diff range defined. Will only spawn when at 0% diff!");
                return;
            }
            string diffStr1 = match.Groups[1].Value;
            string diffStr2 = match.Groups[2].Value;

            MinDiff = float.Parse(diffStr1);
            MaxDiff = float.Parse(diffStr2);
        }

        public override string ToString() => $"{MinDiff} - {MaxDiff}";

        public bool IsInRangeOf(float diff) => MinDiff <= diff && diff < MaxDiff;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MoreLevelContent.Shared.Generation
{
    public abstract class DefWithDifficultyRange : IComparable<DefWithDifficultyRange>
    {
        protected DefWithDifficultyRange(string stringContainingDiff) => DifficultyRange = new DifficultyRange(stringContainingDiff);

        public float MinDifficulty => DifficultyRange.MinDiff;
        public float MaxDifficulrt => DifficultyRange.MaxDiff;

        public DifficultyRange DifficultyRange { get; protected set; }

        public int CompareTo([AllowNull] DefWithDifficultyRange other) => other == null ? -1 : other.MinDifficulty < MinDifficulty ? -1 : other.MinDifficulty == MinDifficulty ? 0 : 1;
    }
}

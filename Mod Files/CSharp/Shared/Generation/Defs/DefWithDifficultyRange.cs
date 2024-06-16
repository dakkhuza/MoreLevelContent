using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MoreLevelContent.Shared.Generation
{
    public abstract class DefWithDifficultyRange : IComparable<DefWithDifficultyRange>
    {
        protected DefWithDifficultyRange(string stringContainingDiff) => DifficultyRange = new DifficultyRange(stringContainingDiff);
        protected DefWithDifficultyRange(float min, float max) => DifficultyRange = new DifficultyRange(min, max);

        public float MinDifficulty => DifficultyRange.MinDiff;
        public float MaxDifficulty => DifficultyRange.MaxDiff;
        public float AverageDifficulty => (MinDifficulty + MaxDifficulty) / 2;

        public DifficultyRange DifficultyRange { get; protected set; }

        public int CompareTo([AllowNull] DefWithDifficultyRange other) => other == null ? -1 : other.MinDifficulty < MinDifficulty ? -1 : other.MinDifficulty == MinDifficulty ? 0 : 1;
    }
}

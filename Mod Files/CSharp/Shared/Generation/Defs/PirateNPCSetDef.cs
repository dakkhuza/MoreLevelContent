using Barotrauma;
using System;
using System.Collections.Generic;
using System.Text;

namespace MoreLevelContent.Shared.Generation
{
    public class PirateNPCSetDef : DefWithDifficultyRange
    {
        internal readonly MissionPrefab Prefab;
        internal PirateNPCSetDef(MissionPrefab prefab, string stringContainingDiff) : base(stringContainingDiff) => Prefab = prefab;
    }
}

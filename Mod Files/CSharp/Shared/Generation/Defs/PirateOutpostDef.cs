using Barotrauma;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Barotrauma.Level;

namespace MoreLevelContent.Shared.Generation
{
    internal class PirateOutpostDef : DefWithDifficultyRange
    {
        internal SubmarineInfo SubInfo;
        internal PlacementType PlacementType;
        internal PirateOutpostDef(SubmarineInfo subInfo, float min, float max, PlacementType placementType) : base(min, max)
        {
            SubInfo = subInfo;
            PlacementType = placementType;
        }
    }
}

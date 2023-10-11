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
        internal SubmarineFile ContentFile;
        internal PlacementType PlacementType;
        internal PirateOutpostDef(SubmarineFile file, DifficultyRange range, PlacementType placement) : base(range)
        {
            ContentFile = file;
            PlacementType = placement;
        }

    }
}

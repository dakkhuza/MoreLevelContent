using Barotrauma;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MoreLevelContent.Shared.Generation
{
    internal class PirateOutpostDef : DefWithDifficultyRange
    {
        internal SubmarineInfo SubInfo;
        internal PirateOutpostDef(SubmarineInfo subInfo, float min, float max) : base(min, max) => SubInfo = subInfo;
    }
}

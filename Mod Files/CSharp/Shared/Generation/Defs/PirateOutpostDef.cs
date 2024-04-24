using Barotrauma;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MoreLevelContent.Shared.Generation
{
    internal class PirateOutpostDef : DefWithDifficultyRange
    {
        internal OutpostModuleFile ContentFile;
        internal PirateOutpostDef(OutpostModuleFile file, SubmarineInfo subInfo) : base(subInfo.OutpostModuleInfo.Name) => ContentFile = file;
    }
}

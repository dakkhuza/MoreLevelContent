using Barotrauma;
using System;
using System.Collections.Generic;
using System.Text;

namespace MoreLevelContent.Shared.Generation.Interfaces
{
    interface ILevelStartGenerate
    {
        internal void OnLevelGenerationStart(LevelData levelData, bool mirror);
    }
}

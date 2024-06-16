
using Barotrauma;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using System.Collections.Generic;
using System.Linq;

namespace MoreLevelContent
{
    /// <summary>
    /// Changes what map feature the current level has.
    /// </summary>
    internal class RevealPirateBase : EventAction
    {
        private bool isFinished = false;
        public RevealPirateBase(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
        }

        public override bool IsFinished(ref string goToLabel) => isFinished;
        public override void Reset() => isFinished = false;
        public override string ToDebugString()
        {
            return $"{ToolBox.GetDebugSymbol(isFinished)} {nameof(RevealMapFeatureAction)}";
        }

    }
}

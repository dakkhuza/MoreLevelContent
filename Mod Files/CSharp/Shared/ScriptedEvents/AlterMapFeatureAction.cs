﻿
using Barotrauma;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;

namespace MoreLevelContent
{
    /// <summary>
    /// Changes what map feature the current level has.
    /// </summary>
    internal class AlterMapFeatureAction : EventAction
    {
        [Serialize("", IsPropertySaveable.Yes, description: "The Identifier this levels map feature should change to.")]
        public Identifier MapFeatureIdentifier { get; set; }

        public AlterMapFeatureAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
            if (MapFeatureIdentifier.IsEmpty)
            {
                DebugConsole.ThrowError($"Error in event \"{parentEvent.Prefab.Identifier}\": MapFeatureIdentifier has not been configured.",
                    contentPackage: element.ContentPackage);
            }
        }

        private bool isFinished = false;
        public override bool IsFinished(ref string goToLabel)
        {
            return isFinished;
        }
        public override void Reset()
        {
            isFinished = false;
        }

        public override void Update(float deltaTime)
        {
            if (isFinished) { return; }
            if (MapFeatureModule.TryGetFeature(MapFeatureIdentifier, out var feature))
            {
                var data = Level.Loaded?.LevelData?.MLC();
                if (data != null)
                {
                    data.MapFeatureData.Name = feature.Name;
                }
            }
            isFinished = true;
        }

        public override string ToDebugString()
        {
            return $"{ToolBox.GetDebugSymbol(isFinished)} {nameof(AlterMapFeatureAction)}";
        }
    }
}
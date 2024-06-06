
using Barotrauma;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;

namespace MoreLevelContent
{
    /// <summary>
    /// Changes what map feature the current level has.
    /// </summary>
    internal class RevealMapFeatureAction : EventAction
    {
        [Serialize("", IsPropertySaveable.Yes, description: "The Identifier of the map feature to search for.")]
        public Identifier MapFeatureIdentifier { get; set; }

        [Serialize(0, IsPropertySaveable.Yes, description: "The max distance this map feature can be away from the current location (1 = one path between locations).")]
        public int MaxDistance { get; set; }

        [Serialize(false, IsPropertySaveable.Yes, description: "If true, a suitable location is forced on the map if one isn't found.")]
        public bool CreateFeatureIfNotFound { get; set; }



        public RevealMapFeatureAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
        }

        public override bool IsFinished(ref string goToLabel) => throw new System.NotImplementedException();
        public override void Reset() => throw new System.NotImplementedException();
    }
}

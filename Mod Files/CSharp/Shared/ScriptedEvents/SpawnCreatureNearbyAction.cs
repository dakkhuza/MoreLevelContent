using Barotrauma;

namespace MoreLevelContent
{
    internal class SpawnCreatureNearbyAction : EventAction
    {
        public SpawnCreatureNearbyAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
        }

        public override bool IsFinished(ref string goToLabel) => throw new System.NotImplementedException();
        public override void Reset() => throw new System.NotImplementedException();
    }
}

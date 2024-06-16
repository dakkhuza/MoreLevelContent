using Barotrauma;
using Barotrauma.Networking;
using MoreLevelContent.Networking;

namespace MoreLevelContent.Shared.Generation
{
    // Client
    abstract partial class TimedEventMapModule
    {
        protected override void InitProjSpecific()
        {
            if (GameMain.IsMultiplayer) NetUtil.Register(EventCreated, CreateEvent);
        }

        

        internal void CreateEvent(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            int id = (int)inMsg.ReadUInt32();
            byte steps = inMsg.ReadByte();
            LocationConnection connection = MapDirector.IdConnectionLookup[id];
            CreateEvent(connection, steps);
        }
    }
}

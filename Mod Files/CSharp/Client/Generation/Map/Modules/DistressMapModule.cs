using Barotrauma.Networking;
using Barotrauma;
using MoreLevelContent.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Shared.Generation
{
    // Client
    internal partial class OldDistressMapModule
    {
        protected override void InitProjSpecific()
        {
            if (GameMain.IsMultiplayer) NetUtil.Register(NetEvent.MAP_SEND_NEWDISTRESS, CreateDistress);
        }

        internal void CreateDistress(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            int id = (int)inMsg.ReadUInt32();
            byte steps = inMsg.ReadByte();
            LocationConnection connection = MapDirector.IdConnectionLookup[id];
            CreateDistress(connection, steps);
        }
    }

    internal partial class DistressMapModule : TimedEventMapModule
    {
    }
}

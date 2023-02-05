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
    internal partial class DistressMapModule
    {
        protected override void InitProjSpecific()
        {
            if (GameMain.IsMultiplayer) NetUtil.Register(NetEvent.MAP_SEND_NEWDISTRESS, CreateDistress);
        }

        internal void CreateDistress(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            int seed = inMsg.ReadInt32();
            Random rand = new MTRandom(seed);
            Shared_CreateDistress(GameMain.GameSession.Map, rand);
        }
    }
}

using Barotrauma.Networking;
using Barotrauma;
using MoreLevelContent.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreLevelContent.Shared.Data;
using Barotrauma.MoreLevelContent.Config;

namespace MoreLevelContent.Shared.Generation
{
    // Server
    internal partial class DistressMapModule
    {
        protected override void InitProjSpecific() => NetUtil.Register(NetEvent.COMMAND_CREATEDISTRESS, Command_CreateDistress);

        internal void Command_CreateDistress(object[] args)
        {
            Log.Debug("Got command");
            ForceDistress();
        }
    }
}

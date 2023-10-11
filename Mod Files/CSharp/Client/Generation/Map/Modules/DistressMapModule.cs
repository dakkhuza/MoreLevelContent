using Barotrauma.Networking;
using Barotrauma;
using MoreLevelContent.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreLevelContent.Shared.Data;

namespace MoreLevelContent.Shared.Generation
{
    // Client
    internal partial class DistressMapModule
    {
        protected override void InitProjSpecific()
        {
            if (!GameMain.IsMultiplayer) return;
            NetUtil.Register(NetEvent.MAP_SEND_NEWDISTRESS, CreateDistress);
            NetUtil.Register(NetEvent.MAP_UPDATE_DISTRESS, UpdateDistress);
        }

        internal void CreateDistress(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            uint id = inMsg.ReadUInt32();
            byte steps = inMsg.ReadByte();
            LocationConnection connection = MapDirector.IdConnectionLookup[id];
            CreateDistress(connection, steps);
        }

        internal void UpdateDistress(object[] args)
        {
            IReadMessage inMsg = (IReadMessage)args[0];
            uint connectionID = inMsg.ReadUInt32();
            bool hasDistress = inMsg.ReadBoolean();
            bool faint = inMsg.ReadBoolean();

            // do stuff to update the distress beacon here
            LocationConnection connection = MapDirector.IdConnectionLookup[connectionID];
            connection.LevelData.MLC().HasDistress = hasDistress;

            if (faint)
            {
                SendDistressUpdate("mlc.distress.faint", connection);
                connection.LevelData.MLC().DistressStepsLeft = 3;
            }
            if (!hasDistress)
            {
                SendDistressUpdate("mlc.distress.lost", connection);
                connection.LevelData.MLC().DistressStepsLeft = 0;
            }
        }
    }
}

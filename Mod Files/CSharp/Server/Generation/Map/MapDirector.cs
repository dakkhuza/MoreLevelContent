using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Shared.Generation
{
    public partial class MapDirector : Singleton<MapDirector>
    {
        partial void SetupProjSpecific()
        {
            NetUtil.Register(NetEvent.MAP_CONNECTION_EQUALITYCHECK_REQUEST, RequestConnectionEquality);
        }
    }
}

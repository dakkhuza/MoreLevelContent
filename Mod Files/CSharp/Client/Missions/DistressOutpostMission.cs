using Barotrauma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    partial class DistressOutpostMission : DistressMission
    {
        public override bool DisplayAsCompleted => false;

        public override bool DisplayAsFailed => false;
    }
}

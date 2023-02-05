using Barotrauma;
using System;
using System.Collections.Generic;
using System.Text;

namespace MoreLevelContent.Missions
{
    partial class BeaconConstMission : Mission
    {
        public override bool DisplayAsCompleted => State > 0;

        public override bool DisplayAsFailed => false;
    }
}

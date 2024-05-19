using Barotrauma;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreLevelContent.Missions
{
    // Client
    internal partial class CablePuzzleMission : Mission
    {
        public override bool DisplayAsCompleted => State == 2;

        public override bool DisplayAsFailed => false;
    }
}

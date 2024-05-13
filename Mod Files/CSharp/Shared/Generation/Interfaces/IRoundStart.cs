using System;
using System.Collections.Generic;
using System.Text;

namespace MoreLevelContent.Shared.Generation.Interfaces
{
    interface IRoundStatus
    {
        void BeforeRoundStart();
        void RoundEnd();
    }
}

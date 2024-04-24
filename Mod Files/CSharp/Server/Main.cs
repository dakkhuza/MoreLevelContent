using Barotrauma;
using System;
using System.Collections.Generic;
using System.Text;

namespace MoreLevelContent
{
    partial class Main
    {
        public static bool IsDedicatedServer => GameMain.Server.OwnerConnection == null;
        public void InitServer()
        {
        }
    }
}

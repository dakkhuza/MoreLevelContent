using System;
using System.Collections.Generic;
using System.Text;

namespace Barotrauma.MoreLevelContent.Shared.Utils
{
    public static class CommandUtils
    {
        public static void AddCommand(string cmdName, string cmdHelp, LuaCsAction callback, LuaCsFunc args = null, bool isCheat = false) => 
            GameMain.LuaCs.Game.AddCommand(cmdName, cmdHelp, callback, args, isCheat);
    }
}

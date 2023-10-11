using Barotrauma.Networking;
using HarmonyLib;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MoreLevelContent
{
    partial class Main
    {
        public void InitServer()
        {
            if (IsDebug)
            {
                MethodInfo gameServer = AccessTools.Method(typeof(GameServer), "Update");
                Patch(gameServer, postfix: new HarmonyMethod(AccessTools.Method(typeof(Main), nameof(Main.GameServer_Update))));
            }
        }

        public static void GameServer_Update(ref float ___endRoundTimer)
        {
            ___endRoundTimer = 0;
        }
    }
}

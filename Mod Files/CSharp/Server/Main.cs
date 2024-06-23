using Barotrauma;
using Barotrauma.Networking;
using HarmonyLib;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace MoreLevelContent
{
    partial class Main
    {
        public static bool IsDedicatedServer => GameMain.Server.OwnerConnection == null;
        public void InitServer()
        {
            Log.Debug("Init Server");
            if (!PreventRoundEnd) return;
            Main.Harmony.Patch(AccessTools.Method(typeof(GameServer), nameof(GameServer.Update)), transpiler: new HarmonyMethod(AccessTools.Method(typeof(Main), nameof(Main.PatchEndRound))));
        }

        static IEnumerable<CodeInstruction> PatchEndRound(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Log.Debug(">>>> Starting end round transpile");
            var code = new List<CodeInstruction>(instructions);
            Log.Debug($"{code.Count}");
            for (int i = 0; i < code.Count; i++)
            {
                if (i >= 514 && i <= 519)
                {
                    Log.Debug($"nop {i}");
                    code[i].opcode = OpCodes.Nop;
                }

                yield return code[i];
            }
        }

        static void Fuck(object arg1, object arg2, object arg3, object arg4, object arg5)
        {
            Log.Debug("Consumed call");
        }

        static void SetRoundEndDelay()
        {
            Log.Debug("called");
            var endRoundDelay = AccessTools.PropertySetter(typeof(GameServer), nameof(GameServer.EndRoundDelay));
            var endRoundTimer = AccessTools.PropertySetter(typeof(GameServer), nameof(GameServer.EndRoundTimer));
            endRoundTimer.Invoke(GameMain.Server, new object[] { 0 });
            endRoundDelay.Invoke(GameMain.Server, new object[] { 1000f });
            Log.Debug($"{GameMain.Server.EndRoundDelay} {GameMain.Server.EndRoundTimer}");
        }
    }
}

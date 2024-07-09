using Barotrauma;
using Barotrauma.Networking;
using HarmonyLib;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace MoreLevelContent
{
    // Server
    partial class Main
    {
        public static bool IsDedicatedServer => GameMain.Server.OwnerConnection == null;
        public static bool CurrentGameModeValid = true;
        public void InitServer()
        {
            Log.Debug("Init Server");
            Harmony.Patch(AccessTools.PropertySetter(typeof(NetLobbyScreen), nameof(NetLobbyScreen.SelectedModeIndex)), postfix: new HarmonyMethod(AccessTools.Method(typeof(Main), nameof(Main.OnGameModeChange))));
            OnGameModeChange(GameMain.NetLobbyScreen);
            if (PreventRoundEnd)
            {
                Main.Harmony.Patch(AccessTools.Method(typeof(GameServer), nameof(GameServer.Update)), transpiler: new HarmonyMethod(AccessTools.Method(typeof(Main), nameof(Main.PatchEndRound))));
            }
        }

        static IEnumerable<CodeInstruction> PatchEndRound(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Log.Debug(">>>> Starting end round transpile");
            var code = new List<CodeInstruction>(instructions);
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

        static void OnGameModeChange(NetLobbyScreen __instance)
        {
            var gameMode = __instance.GameModes[__instance.SelectedModeIndex];
            CurrentGameModeValid = gameMode.GameModeType == typeof(MultiPlayerCampaign);
            var validClients = GameMain.Server.ConnectedClients.Where(c => c.HasPermission(ClientPermissions.SelectMode));
            if (!CurrentGameModeValid)
            {
                foreach (var client in validClients)
                {
                    GameMain.Server.SendDirectChatMessage(
                        TextManager.GetServerMessage($"mlc.gamemodewarning.description").Value,
                        client,
                        ChatMessageType.ServerMessageBox);
                }
            }
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

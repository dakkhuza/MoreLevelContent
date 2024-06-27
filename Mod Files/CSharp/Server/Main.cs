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

        internal static void OnClientInstallCheck(Client client)
        {
            if (CurrentGameModeValid) return;
            if (client.HasPermission(ClientPermissions.ManageSettings) || client.Connection == GameMain.Server.OwnerConnection)
            {
                GameMain.Server.SendDirectChatMessage(
                    TextManager.GetServerMessage($"mlc.gamemodewarning.description").Value,
                    client,
                    ChatMessageType.ServerMessageBox);
                return;
            }
        }

        static void OnGameModeChange(NetLobbyScreen __instance)
        {
            var gameMode = __instance.GameModes[__instance.SelectedModeIndex];
            if (gameMode.GameModeType != typeof(MultiPlayerCampaign))
            {
                CurrentGameModeValid = false;
                return;
            }
            CurrentGameModeValid = true;
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

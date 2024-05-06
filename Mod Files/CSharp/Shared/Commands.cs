using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using System.Linq;

namespace MoreLevelContent
{
    public class Commands : Singleton<Commands>
    {
        public override void Setup()
        {
            CommandUtils.AddCommand("mlc_debugmissions", "Prints a debug output of all active missions", _debugMissions);
            CommandUtils.AddCommand("mlc_stepworld", "Fakes a world step", _stepWorld, isCheat: true);
            CommandUtils.AddCommand("mlc_createdistressbeacon", "Tries to create a new distress beacon", _createDistress, isCheat: true);
            CommandUtils.AddCommand("mlc_forcedistress", "Toggles forcing every level to spawn a distress mission, does nothing in multiplayer", _forceDistress, isCheat: true);
            // CommandUtils.AddCommand("mlc_togglemapteleport", "Toggles debug teleporting on the campaign map", _toggleMapTP);
        }

        private void _debugMissions(object[] args)
        {
            foreach (var item in GameMain.GameSession.Missions)
            {
                Log.Debug(
                    $"MISSION DEBUG PRINTOUT\n" +
                    $"Name: {item.Name.Value}\n" +
                    $"Sonar: {item.SonarLabels.FirstOrDefault().Label}\n" +
                    $"Sonar Position: {item.SonarLabels.FirstOrDefault().Position}\n" +
                    $"Beacon: {Level.Loaded.MLC().BeaconConstructionStation.WorldPosition}");
            }
        }

        private void _forceDistress(object[] args)
        {//ForcedMissionIdentifier
            string additional = "";
            string[] arg = (string[])args[0];

            if (arg.Length > 0 && arg[0].ToString().Length > 0)
            {
                DistressMapModule.ForceSpawnDistress = true;
                DistressMapModule.ForcedMissionIdentifier = arg[0].ToString();
                additional = " , forced to: " + DistressMapModule.ForcedMissionIdentifier;
            } else
            {
                DistressMapModule.ForceSpawnDistress = !DistressMapModule.ForceSpawnDistress;
                if (!DistressMapModule.ForceSpawnDistress)
                {
                    DistressMapModule.ForcedMissionIdentifier = "";
                }
            }

            DebugConsole.NewMessage((DistressMapModule.ForceSpawnDistress ? "Enabled" : "Disabled") + " forceing of distress mission" + additional, Color.White);
        }

        private void _stepWorld(object[] args)
        {
            if (!Main.IsCampaign) return;
#if CLIENT
            NetUtil.SendServer(NetUtil.CreateNetMsg(NetEvent.COMMAND_STEPWORLD));
            MapDirector.ForceWorldStep();
#endif
        }

        private void _createDistress(object[] args)
        {
            if (!Main.IsCampaign)
            {
                Log.Error($"Can't create a distress beacon when not in campaign! ({GameMain.GameSession?.Campaign != null} || {GameMain.IsSingleplayer}) -> {GameMain.GameSession?.Campaign != null || GameMain.IsSingleplayer}");
                return;
            }
            Log.Debug("Creating distress");
            if (Main.IsClient) _createDistressClient();
            else DistressMapModule.ForceDistress();
        }


        void _createDistressClient()
        {
#if CLIENT
            if (!GameMain.Client.HasPermission(Barotrauma.Networking.ClientPermissions.ManageRound))
            {
                Log.Error("No Perms");
                return;
            }
            Log.Debug("Sent create distress request");
            NetUtil.SendServer(NetUtil.CreateNetMsg(NetEvent.COMMAND_CREATEDISTRESS));
#endif
        }
    }
}

using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Generation.Pirate;
using MoreLevelContent.Shared.Store;
using System.Linq;

namespace MoreLevelContent
{
    public class Commands : Singleton<Commands>
    {
        public static bool DisplayAllMapLocations = false;

        public override void Setup()
        {
            CommandUtils.AddCommand("mlc_debugmissions", "Prints a debug output of all active missions", _debugMissions);
            CommandUtils.AddCommand("mlc_dumppirateoutposts", "Dumps the file paths of all pirate outposts", _dumpPirateOutposts, isCheat: true);
            CommandUtils.AddCommand("mlc_stepworld", "Fakes a world step", _stepWorld, isCheat: true);
            CommandUtils.AddCommand("mlc_createdistressbeacon", "Tries to create a new distress beacon", _createDistress, isCheat: true);
            CommandUtils.AddCommand("mlc_forcedistress", "Toggles forcing every level to spawn a distress mission, does nothing in multiplayer", _forceDistress, isCheat: true);
            CommandUtils.AddCommand("mlc_forcepirate", "Toggles forcing a specific pirate base to spawn", _forcePirate, isCheat: true);
            CommandUtils.AddCommand("mlc_toggleMapDisplay", "Toggles if all map locations should be shown, even if they are not discovered yet", _toggleMapDisplay, isCheat: true);
            CommandUtils.AddCommand("mlc_showpatchnotes", "Displays the patch notes", _showPatchnotes);
        }

        private void _showPatchnotes(object[] args)
        {
#if CLIENT
            Barotrauma.MoreLevelContent.Client.UI.PatchNotes.Open();
#endif
        }

        private void _toggleMapDisplay(object[] args) => DisplayAllMapLocations = !DisplayAllMapLocations;

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
            string identifier = arg.Length == 2 ? arg[1] : "";

            if (arg.Length == 0) return;
            if (!bool.TryParse(arg[0], out bool force)) return;
            MapDirector.Instance.SetForcedDistressMission(force, identifier);


            DebugConsole.NewMessage((force ? "Enabled" : "Disabled") + " forceing of distress mission" + additional, Color.White);
        }

        private void _forcePirate(object[] args)
        {//ForcedMissionIdentifier
            string additional = "";
            string[] arg = (string[])args[0];
            string identifier = arg.Length == 2 ? arg[1] : "";

            if (arg.Length == 0) return;
            if (!bool.TryParse(arg[0], out bool force)) return;

            PirateOutpostDirector.Instance.ForceSpawn = force;
            PirateOutpostDirector.Instance.ForcedPirateOutpost = identifier;
            if (!identifier.IsNullOrEmpty())
            {
                additional = ", all outpost will be " + identifier;
            }
            if (!force)
            {
                PirateOutpostDirector.Instance.ForcedPirateOutpost = "";
                additional = ", all outpost will be random";
            }

            DebugConsole.NewMessage((force ? "Enabled" : "Disabled") + " forceing of pirate outposts" + additional, Color.White);
        }

        private void _dumpPirateOutposts(object[] args)
        {
            PirateStore.Instance.DumpPirateOutposts();
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
            else OldDistressMapModule.ForceDistress();
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

using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Generation.Pirate;
using MoreLevelContent.Shared.Store;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

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
            CommandUtils.AddCommand("mlc_leveldatadebug", "Displays debug info on the current level's generation data", _isDistressActive);
            CommandUtils.AddCommand("mlc_itemspotcheck", "", _itemSpotCheck, isCheat: true);
            CommandUtils.AddCommand("mlc_togglelaggymotiondetectors", "", _motionToggle, isCheat: true);
        }

        private void _motionToggle(object[] args)
        {
            Log.Debug($"Checking {Item.ItemList.Count} items...");
            Stopwatch sw = new Stopwatch();
            Stopwatch totalTime = new Stopwatch();
            int duration = 500;
            totalTime.Start();
            sw.Start();
            foreach (Item item in Item.ItemList)
            {
                item.Update((float)(Timing.Step), GameMain.GameScreen.Cam);
                if (sw.ElapsedTicks > duration)
                {
                    MotionSensor sensor = item.GetComponent<MotionSensor>();
                    if (sensor == null) continue;
                    Log.Debug($"Disabling item: {item.Name} : {item.Prefab.Identifier} width: {sensor.RangeX} height: {sensor.RangeY} with interval {sensor.UpdateInterval} (in room {item.CurrentHull?.RoomName})");
                    sensor.UpdateInterval = 1;
                }

                sw.Restart();
            }
        }

        private void _itemSpotCheck(object[] args)
        {
            Log.Debug("Preforming spot check...");
            string[] arg = (string[])args[0];
            int duration = 1;
            if (arg.Length > 0 && !int.TryParse(arg[0], out duration));
            bool listAll = false;
            if (arg.Length > 0 && !bool.TryParse(arg[0], out listAll));
            Log.Debug($"Checking {Item.ItemList.Count} items...");
            Stopwatch sw = new Stopwatch();
            Stopwatch totalTime = new Stopwatch();
            totalTime.Start();
            sw.Start();
            foreach (Item item in Item.ItemList)
            {
                item.Update((float)(Timing.Step), GameMain.GameScreen.Cam);
                if (sw.ElapsedTicks > duration || listAll)
                {
                    Log.Debug($"-- Item: {item.Name} : {item.Prefab.Identifier} (in room {item.CurrentHull?.RoomName}) from package {item.Prefab.ContentPackage.Name} took {sw.ElapsedTicks} to update!");
                }
                
                sw.Restart();
            }

            Log.Debug($"Done: {totalTime.ElapsedMilliseconds}!");
        }

        private void _isDistressActive(object[] args)
        {
            if (Level.Loaded == null)
            {
                Log.Debug("No level loaded");
                return;
            }
            Log.Debug($"HasDistress: {Level.Loaded.LevelData.MLC().HasDistress}, Steps left: {Level.Loaded.LevelData.MLC().DistressStepsLeft}");
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
            if (GameMain.IsMultiplayer) return;
            string additional = "";
            string[] arg = (string[])args[0];
            string identifier = arg.Length == 2 ? arg[1] : "";

            if (arg.Length == 0)
            {
                Log.Debug("Missing arguments");
                return;
            }
            if (!bool.TryParse(arg[0], out bool force))
            {
                Log.Debug("First argument must be a boolean");
                return;
            }
            MapDirector.Instance.SetForcedDistressMission(force, identifier);


            DebugConsole.NewMessage((force ? "Enabled" : "Disabled") + " forceing of distress mission " + identifier, Color.White);
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
            if (Main.IsClient) return;
            Log.Debug("Creating distress");
            MapDirector.Instance.ForceDistress();
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

using Barotrauma.MoreLevelContent.Client.UI;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using Barotrauma.MoreLevelContent.Shared.Config;
using MoreLevelContent;
using Barotrauma.Networking;
using MoreLevelContent.Networking;

namespace Barotrauma.MoreLevelContent.Config
{
    /// <summary>
    /// Client
    /// </summary>
    partial class ConfigManager : Singleton<ConfigManager>
    {
        private void SetupClient()
        {
            CommandUtils.AddCommand(
                "mlc_config",
                "Toggle the display of the config editor",
                ToggleGUI);
            // Exit if we're in an editor
            if (Screen.Selected.IsEditor) return;
            if (GameMain.IsSingleplayer) return; // We don't need to do any of this if we're in singleplayer
            NetUtil.Register(NetEvent.CONFIG_WRITE_CLIENT, ClientRead);
            if (!GameMain.Client.IsServerOwner) RequestConfig();
            else ClientWrite();
        }

        public void SetConfig(MLCConfig config)
        {
            this.Config = config;
            Log.Debug("[CLIENT] Config Updated");
            Log.Verbose(Config.ToString());

            if (!GameMain.IsSingleplayer) UpdateConfig();
            SaveConfig();
        }

        private void RequestConfig()
        {
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_REQUEST);
            outMsg.WriteString(Main.Version);
            GameMain.LuaCs.Networking.Send(outMsg);
            Log.Verbose("Requested config from server...");
        }

        private void ClientWrite()
        {
            // Always allow the server owner to write
            if (!GameMain.Client.HasPermission(ClientPermissions.ManageSettings) && !GameMain.Client.IsServerOwner)
            {
                Log.Error("No Perms!");
                return;
            }
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_WRITE_SERVER);
            outMsg.WriteString(Main.Version);
            WriteConfig(ref outMsg);
            GameMain.LuaCs.Networking.Send(outMsg);
            Log.Debug("Sent config packet to server!");
        }

        private void ClientRead(object[] args)
        {
            Log.Debug("Got config packet!");
            IReadMessage inMsg = (IReadMessage)args[0];
            ReadNetConfig(ref inMsg);
        }

        private void UpdateConfig()
        {
            if (!GameMain.Client.HasPermission(ClientPermissions.ManageSettings)) return;
            ClientWrite();
        }

        private void DisplayPatchNotes(bool force = false)
        {
            
            if (Config.Version != Main.Version || force || Main.IsNightly)
            {
                PatchNotes.Open();
            }
        }

        public bool SettingsOpen
        {
            get => _settingsOpen;
            set
            {
                if (value == _settingsOpen) { return; }

                if (value)
                {
                    
                    _settingsMenu = new GUIFrame(new RectTransform(Vector2.One, Screen.Selected.Frame.RectTransform, Anchor.Center), style: null);
                    _ = new GUIFrame(new RectTransform(GUI.Canvas.RelativeSize, _settingsMenu.RectTransform, Anchor.Center), style: "GUIBackgroundBlocker");

                    var settingsMenuInner = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.8f), _settingsMenu.RectTransform, Anchor.Center, scaleBasis: ScaleBasis.Smallest) { MinSize = new Point(640, 480) });
                    _ = ConfigMenu.Create(settingsMenuInner.RectTransform);
                    Log.Verbose("Opened Settings");
                }
                else
                {
                    ConfigMenu.Instance?.Close();
                    _settingsMenu.Parent.RemoveChild(_settingsMenu);
                    _settingsMenu = null;
                    Log.Verbose("Closed Settings");
                }
                _settingsOpen = value;
            }
        }
        private static bool _settingsOpen;
        private static GUIFrame _settingsMenu;
        private void ToggleGUI(object[] args) => SettingsOpen = !SettingsOpen;
    }
}

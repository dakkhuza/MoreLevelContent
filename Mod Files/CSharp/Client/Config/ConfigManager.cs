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
            LoadConfig();
            NetUtil.Register(NetEvent.CONFIG_WRITE_CLIENT, ClientRead);
            NetUtil.Register(NetEvent.PING_CLIENT, PongServer);
            if (!GameMain.Client.IsServerOwner) RequestConfig();
            else ClientWrite();
        }

        public void SetConfig(MLCConfig config)
        {
            this.config = config;
            Log.Debug("[CLIENT] Config Updated");
            Log.Verbose(Config.ToString());

            // Rework this to be better
            UpdateConfig();
            SaveConfig();
        }

        private void RequestConfig()
        {
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.CONFIG_REQUEST);
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
            Config.WriteTo(ref outMsg);
            GameMain.LuaCs.Networking.Send(outMsg);
            Log.Debug("Sent config packet to server!");
        }

        private void ClientRead(object[] args)
        {
            Log.Debug("Got config packet!");
            IReadMessage inMsg = (IReadMessage)args[0];
            ReadNetConfig(ref inMsg);
        }

        private void PongServer(object[] args)
        {
            IWriteMessage outMsg = NetUtil.CreateNetMsg(NetEvent.PONG_SERVER);
            outMsg.Write(Main.Version);
            NetUtil.SendServer(outMsg);
            Log.Debug("Pong!");
        }

        private void UpdateConfig()
        {
            if (!GameMain.Client.HasPermission(ClientPermissions.ManageSettings)) return;
            ClientWrite();
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

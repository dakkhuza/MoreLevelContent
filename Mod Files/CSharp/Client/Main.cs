using Barotrauma;
using Barotrauma.MoreLevelContent.Client;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MoreLevelContent
{
    /// <summary>
    /// Client
    /// </summary>
    partial class Main
    {
        private GUIButton SettingsButton;
        private FieldInfo traitorProbabilityText;
        public void InitClient()
        {
            // var gameMain_ResetNetLobbyScreen = typeof(GameMain).GetMethod(nameof(GameMain.ResetNetLobbyScreen), BindingFlags.Static | BindingFlags.Public);
            traitorProbabilityText = typeof(NetLobbyScreen).GetField("traitorProbabilityText", BindingFlags.Instance | BindingFlags.NonPublic);
            CreateSettingsButton();
            // HookMethod("mlc.client.CreateSettingsButton", gameMain_ResetNetLobbyScreen, CreateSettingsButton, LuaCsHook.HookMethodType.After);
        }

        private void CreateSettingsButton()
        {
            GUITextBlock textBox =  (GUITextBlock)traitorProbabilityText.GetValue(GameMain.NetLobbyScreen);
            var settingsContentRect = textBox.RectTransform
                .Parent // traitorProbContainer
                .Parent // traitorsSettingHolder
                .Parent // settingsContent
                ;
            var mlcSettingHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.1f), settingsContentRect), isHorizontal: true, childAnchor: Anchor.CenterLeft) { Stretch = true };
            SettingsButton = new GUIButton(new RectTransform(new Vector2(0.25f, 1.0f), mlcSettingHolder.RectTransform, Anchor.TopRight),
                TextManager.Get("mlc.config"))
            {
                OnClicked = OpenConfig
            };
            Log.Debug("Created settings button");
        }

        private bool OpenConfig(GUIButton button, object obj)
        {
            ConfigManager.Instance.SettingsOpen = true;
            return false;
        }
    }
}

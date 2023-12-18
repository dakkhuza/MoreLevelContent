using Barotrauma;
using Barotrauma.MoreLevelContent.Client;
using Barotrauma.MoreLevelContent.Client.UI;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.Networking;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
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
            MapUI.Instance.Setup();

            // Exit if we're in an editor 
            if (Screen.Selected.IsEditor) return;

            MethodInfo info = typeof(GUI).GetMethod("TogglePauseMenu", BindingFlags.Static | BindingFlags.Public);
            Patch(info, postfix: new HarmonyMethod(AccessTools.Method(typeof(Main), "AddSettingsButton")));

            // or single player
            if (GameMain.IsSingleplayer) return;

            traitorProbabilityText = typeof(NetLobbyScreen).GetField("traitorProbabilityText", BindingFlags.Instance | BindingFlags.NonPublic);
            //CreateSettingsButton();
        }



        private static void AddSettingsButton()
        {
            if (!GUI.PauseMenuOpen) return; // don't try to add the button when the pause menu doesn't exist
            var target = GUI.PauseMenu.Children.ToList()[1].Children.First();
            var button = new GUIButton(new RectTransform(new Vector2(1, 0.1f), target.RectTransform), TextManager.Get("mlc.configshort"), style: "GUIButtonSmall")
            {
                OnClicked = (GUIButton obj, object o) => 
                {
                    GUI.TogglePauseMenu();
                    return Instance.OpenConfig(obj, o); 
                },
            };
        }

        private void CreateSettingsButton()
        {
            if (SettingsButton != null) return; // Exit if the settings button is already created
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

﻿using Barotrauma;
using Barotrauma.MoreLevelContent.Client.UI;
using Barotrauma.MoreLevelContent.Config;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Utils;
using System.Linq;
using System.Reflection;

namespace MoreLevelContent
{
    /// <summary>
    /// Client
    /// </summary>
    partial class Main
    {
        public void InitClient()
        {
            MapUI.Instance.Setup();
            Hooks.Instance.OnDebugDraw += ClientDebugDraw.Draw;
            SonarExtensions.Instance.Setup();

            GameMain.LuaCs.Hook.Add("roundStart", OpenPatchNotes);

            // Exit if we're in an editor 
            if (Screen.Selected.IsEditor) return;
            MethodInfo info = typeof(GUI).GetMethod("TogglePauseMenu", BindingFlags.Static | BindingFlags.Public);
            Patch(info, postfix: new HarmonyMethod(AccessTools.Method(typeof(Main), "AddSettingsButton")));
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

        object OpenPatchNotes(object[] args)
        {
            if (!ConfigManager.ShouldDisplayPatchNotes) return null;
            CoroutineManager.Invoke(() => PatchNotes.Open(), delay: 5.0f);
            return null;
        }

        private bool OpenConfig(GUIButton button, object obj)
        {
            ConfigManager.Instance.SettingsOpen = true;
            return false;
        }
    }
}

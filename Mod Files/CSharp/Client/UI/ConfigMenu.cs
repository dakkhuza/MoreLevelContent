using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Barotrauma.MoreLevelContent.Shared.Config;
using Barotrauma.MoreLevelContent.Config;
using MoreLevelContent.Shared;
using OpenAL;

namespace Barotrauma.MoreLevelContent.Client.UI
{
    public class ConfigMenu
    {
        public static ConfigMenu Instance { get; private set; }

        private MLCConfig unsavedConfig;

        private readonly GUIFrame mainFrame;
        private readonly GUILayoutGroup tabber;
        private readonly GUIFrame contentFrame;
        private readonly GUILayoutGroup bottom;
        private readonly Dictionary<Tab, (GUIButton Button, GUIFrame Content)> tabContents;

        public ConfigMenu(RectTransform mainParent)
        {
            unsavedConfig = ConfigManager.Instance.Config;
            mainFrame = new GUIFrame(new RectTransform(Vector2.One, mainParent));
            var mainLayout = new GUILayoutGroup(new RectTransform(Vector2.One * 0.95f, mainFrame.RectTransform, Anchor.Center, Pivot.Center),
                                isHorizontal: false, childAnchor: Anchor.TopRight);

            _ = new GUITextBlock(new RectTransform((1.0f, 0.07f), mainLayout.RectTransform), TextManager.Get("mlc.config"),
                    font: GUIStyle.LargeFont);

            var tabberAndContentLayout = new GUILayoutGroup(new RectTransform((1.0f, 0.86f), mainLayout.RectTransform),
                isHorizontal: true);

            void tabberPadding()
                => new GUIFrame(new RectTransform((0.01f, 1.0f), tabberAndContentLayout.RectTransform), style: null);


            tabberPadding();
            tabber = new GUILayoutGroup(new RectTransform((0.06f, 1.0f), tabberAndContentLayout.RectTransform), isHorizontal: false) { AbsoluteSpacing = GUI.IntScale(5f) };
            tabberPadding();
            tabContents = new Dictionary<Tab, (GUIButton Button, GUIFrame Content)>();

            contentFrame = new GUIFrame(new RectTransform((0.92f, 1.0f), tabberAndContentLayout.RectTransform),
                style: "InnerFrame");
            bottom = new GUILayoutGroup(new RectTransform((contentFrame.RectTransform.RelativeSize.X, 0.04f), mainLayout.RectTransform), isHorizontal: true) { Stretch = true, RelativeSpacing = 0.01f };

            var tabToSelect = Tab.Debug;
            tabToSelect = MakePermissionLockedTabs(tabToSelect);
            CreateDebugTab();

            CreateBottomButtons();

            SelectTab(tabToSelect);
        }

        private Tab MakePermissionLockedTabs(Tab defaultTab)
        {
            // If we're not in single player
            if (!GameMain.IsSingleplayer)
            {
                // and we don't have perms
                if (!GameMain.Client.HasPermission(Networking.ClientPermissions.ManageSettings)) 
                    return defaultTab; // don't create perm locked tabs
            }
            CreateGeneralTab();
            CreatePirateOutpostTab();
            return Tab.General;
        }

        GUITextBlock moveRuinsChanceDisplay;
        GUITextBlock maxDistressCountDisplay;
        GUITextBlock distressSpawnChanceDisplay;
        private void CreateGeneralTab()
        {
            GUIFrame content = CreateNewContentFrame(Tab.General);
            var (left, right) = CreateSidebars(content);

            Tickbox(left,
                TextManager.Get("mlc.settings.enablethalamuscave"),
                TextManager.Get("mlc.settings.enablethalamuscavetooltip"),
                unsavedConfig.NetworkedConfig.GeneralConfig.EnableThalamusCaves,
                (v) => unsavedConfig.NetworkedConfig.GeneralConfig.EnableThalamusCaves = v);


            var moveRuinsChance = Label(left, TextManager.Get("mlc.settings.moveruins"), GUIStyle.SubHeadingFont);
            moveRuinsChanceDisplay = TextBlock(moveRuinsChance, TextManager.Get("mlc.settings.moveruinstooltip"));
            Slider(left, (0, 100), 100, (v) => $"{Round(v)}%",
                unsavedConfig.NetworkedConfig.GeneralConfig.RuinMoveChance,
                (v) => UpdateRuinMoveChance(v));

            var maxActiveDistress = Label(left, TextManager.Get("mlc.settings.maxdistresscount"), GUIStyle.SubHeadingFont);
            maxDistressCountDisplay = TextBlock(maxActiveDistress, TextManager.Get("mlc.settings.maxdistresscounttooltip"));
            Slider(left, (0, 100), 100, (v) => $"{Round(v)}",
                    unsavedConfig.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons,
                    (v) => UpdateMaxDistress(v));


            var distressSpawnChance = Label(left, TextManager.Get("mlc.settings.spawndistresschance"), GUIStyle.SubHeadingFont);
            distressSpawnChanceDisplay = TextBlock(distressSpawnChance, TextManager.Get("mlc.settings.spawndistresschancetooltip"));
            Slider(left, (0, 100), 100, (v) => $"{Round(v)}",
                unsavedConfig.NetworkedConfig.GeneralConfig.DistressSpawnChance,
                (v) => UpdateDistressSpawnChance(v));


            void UpdateRuinMoveChance(float v)
            {
                unsavedConfig.NetworkedConfig.GeneralConfig.RuinMoveChance = Round(v);
                moveRuinsChanceDisplay.Text = TextManager.GetWithVariable("mlc.settings.spawnchance", "[chance]", Round(v).ToString()); ;
            }

            void UpdateMaxDistress(float v)
            {
                unsavedConfig.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons = Round(v);
                maxDistressCountDisplay.Text = TextManager.GetWithVariable("mlc.settings.maxactive", "[max]", Round(v).ToString()); ;
            }

            void UpdateDistressSpawnChance(float v)
            {
                unsavedConfig.NetworkedConfig.GeneralConfig.DistressSpawnChance = Round(v);
                distressSpawnChanceDisplay.Text = TextManager.GetWithVariable("mlc.settings.spawnchance", "[chance]", Round(v).ToString());
            }


            UpdateRuinMoveChance(unsavedConfig.NetworkedConfig.GeneralConfig.RuinMoveChance);
            UpdateMaxDistress(unsavedConfig.NetworkedConfig.GeneralConfig.MaxActiveDistressBeacons);
            UpdateDistressSpawnChance(unsavedConfig.NetworkedConfig.GeneralConfig.DistressSpawnChance);

            GUITextBlock TextBlock(GUITextBlock container, RichString tooltip)
            {
                return new GUITextBlock(new RectTransform(new Vector2(1.0f, 1.0f), container.RectTransform), "", textAlignment: Alignment.CenterRight)
                {
                    ToolTip = tooltip
                };
            }
        }

        GUITextBlock pirateSpawnChanceDisplay;

        private void CreatePirateOutpostTab()
        {
            GUIFrame content = CreateNewContentFrame(Tab.PirateOutpost);
            var (left, right) = CreateSidebars(content);

            // If the pirate outpost is displayed on sonar
            Tickbox(left, 
                TextManager.Get("mlc.config.piratedisplaysonar"), 
                TextManager.Get("mlc.config.piratedisplaysonartooltip"),
                unsavedConfig.NetworkedConfig.PirateConfig.DisplaySonarMarker, 
                (v) => unsavedConfig.NetworkedConfig.PirateConfig.DisplaySonarMarker = v);

            // If the pirate difficulty should scale with server memebers
            Tickbox(left,
                TextManager.Get("mlc.config.piratescalediff"),
                TextManager.Get("mlc.config.piratescaledifftooltip"),
                unsavedConfig.NetworkedConfig.PirateConfig.AddDiffPerPlayer,
                (v) => unsavedConfig.NetworkedConfig.PirateConfig.AddDiffPerPlayer = v);
        }

        private void CreateDebugTab()
        {
            GUIFrame content = CreateNewContentFrame(Tab.Debug);
            var (left, right) = CreateSidebars(content);

            Tickbox(left, TextManager.Get("mlc.config.debugverbose"), TextManager.Get("mlc.config.debugverbosetooltip"), unsavedConfig.Client.Verbose, (v) => unsavedConfig.Client.Verbose = v);
            Tickbox(left, TextManager.Get("mlc.config.debuginternal"), TextManager.Get("mlc.config.debuginternaltooltip"), unsavedConfig.Client.Internal, (v) => unsavedConfig.Client.Internal = v);
            
            GUIButton showPatchNotes = new GUIButton(NewItemRectT(left), text: "Patch Notes")
            {
                OnClicked = (btn, obj) =>
                {
                    MoreLevelContent.Client.UI.PatchNotes.Open();
                    return false;
                }
            };
        }

        private void CreateBottomButtons()
        {
            GUIButton cancelButton =
                new GUIButton(new RectTransform(new Vector2(1.0f, 1.0f), bottom.RectTransform), text: "Cancel")
                {
                    OnClicked = (btn, obj) =>
                    {
                        Close();
                        return false;
                    }
                };
            GUIButton applyButton =
                new GUIButton(new RectTransform(new Vector2(1.0f, 1.0f), bottom.RectTransform), text: "Apply")
                {
                    OnClicked = (btn, obj) =>
                    {
                        ConfigManager.Instance.SetConfig(unsavedConfig);
                        mainFrame.Flash(color: GUIStyle.Green);
                        return false;
                    }
                };
        }

        private void Tickbox(GUILayoutGroup parent, LocalizedString label, LocalizedString tooltip, bool currentValue, Action<bool> setter)
        {
            var tickbox = new GUITickBox(NewItemRectT(parent), label)
            {
                Selected = currentValue,
                ToolTip = tooltip,
                OnSelected = (tb) =>
                {
                    setter(tb.Selected);
                    return true;
                }
            };
        }

        private int Round(float v) => (int)MathF.Round(v);

        private void Slider(GUILayoutGroup parent, Vector2 range, int steps, Func<float, string> labelFunc, float currentValue, Action<float> setter, LocalizedString tooltip = null)
        {
            var layout = new GUILayoutGroup(NewItemRectT(parent), isHorizontal: true);
            var slider = new GUIScrollBar(new RectTransform((0.82f, 1.0f), layout.RectTransform), style: "GUISlider")
            {
                Range = range,
                BarScrollValue = currentValue,
                Step = 1.0f / (steps - 1),
                BarSize = 1.0f / steps
            };
            if (tooltip != null)
            {
                slider.ToolTip = tooltip;
            }
            var label = new GUITextBlock(new RectTransform((0.18f, 1.0f), layout.RectTransform),
                labelFunc(currentValue), wrap: false, textAlignment: Alignment.Center);
            slider.OnMoved = (sb, val) =>
            {
                label.Text = labelFunc(sb.BarScrollValue);
                setter(sb.BarScrollValue);
                return true;
            };
        }

        private static GUITextBlock Label(GUILayoutGroup parent, LocalizedString str, GUIFont font) => new GUITextBlock(NewItemRectT(parent), str, font: font);
        private static RectTransform NewItemRectT(GUILayoutGroup parent)
             => new RectTransform((1.0f, 0.06f), parent.RectTransform, Anchor.CenterLeft);

        private static (GUILayoutGroup Left, GUILayoutGroup Right) CreateSidebars(GUIFrame parent, bool split = false)
        {
            GUILayoutGroup layout = new GUILayoutGroup(new RectTransform(Vector2.One, parent.RectTransform), isHorizontal: true);
            GUILayoutGroup left = new GUILayoutGroup(new RectTransform((0.4875f, 1.0f), layout.RectTransform), isHorizontal: false);
            var centerFrame = new GUIFrame(new RectTransform((0.025f, 1.0f), layout.RectTransform), style: null);
            if (split)
            {
                _ = new GUICustomComponent(new RectTransform(Vector2.One, centerFrame.RectTransform),
                    onDraw: (sb, c) => sb.DrawLine((c.Rect.Center.X, c.Rect.Top), (c.Rect.Center.X, c.Rect.Bottom), GUIStyle.TextColorDim, 2f));
            }
            GUILayoutGroup right = new GUILayoutGroup(new RectTransform((0.4875f, 1.0f), layout.RectTransform), isHorizontal: false);
            return (left, right);
        }

        private GUIFrame CreateNewContentFrame(Tab tab)
        {
            var content = new GUIFrame(new RectTransform(Vector2.One * 0.95f, contentFrame.RectTransform, Anchor.Center, Pivot.Center), style: null);
            AddButtonToTabber(tab, content);
            return content;
        }

        private void AddButtonToTabber(Tab tab, GUIFrame content)
        {
            var button = new GUIButton(new RectTransform(Vector2.One, tabber.RectTransform, Anchor.TopLeft, Pivot.TopLeft, scaleBasis: ScaleBasis.Smallest), "", style: $"SettingsMenuTab.{tab}")
            {
                ToolTip = TextManager.Get($"SettingsTab.{tab}"),
                OnClicked = (b, _) =>
                {
                    SelectTab(tab);
                    return false;
                }
            };
            button.RectTransform.MaxSize = RectTransform.MaxPoint;
            button.Children.ForEach(c => c.RectTransform.MaxSize = RectTransform.MaxPoint);

            tabContents.Add(tab, (button, content));
        }

        public void SelectTab(Tab tab)
        {
            SwitchContent(tabContents[tab].Content);
            tabber.Children.ForEach(c =>
            {
                if (c is GUIButton btn) { btn.Selected = btn == tabContents[tab].Button; }
            });
        }

        private void SwitchContent(GUIFrame newContent)
        {
            contentFrame.Children.ForEach(c => c.Visible = false);
            newContent.Visible = true;
        }

        public static ConfigMenu Create(RectTransform mainParent)
        {
            Instance?.Close();
            Instance = new ConfigMenu(mainParent);
            return Instance;
        }

        public void Close()
        {
            mainFrame.Parent.RemoveChild(mainFrame);
            Instance = null;
            ConfigManager.Instance.SettingsOpen = false;
        }

        public enum Tab
        {
            General,
            PirateOutpost,
            Debug
        }
    }
}

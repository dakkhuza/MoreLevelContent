using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Barotrauma.MoreLevelContent.Shared.Config;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma;
using MoreLevelContent;

namespace Barotrauma.MoreLevelContent.Client.UI
{
    public class PatchNotes
    {
        private readonly GUIFrame mainFrame;
        private readonly GUIFrame backgroundBlocker;
        private readonly GUIFrame contentFrame;
        private readonly GUILayoutGroup bottom;
        public static PatchNotes Instance { get; private set; }

        public PatchNotes()
        {
            backgroundBlocker = new GUIFrame(new RectTransform(Vector2.One, Screen.Selected.Frame.RectTransform, Anchor.Center), style: null);
            _ = new GUIFrame(new RectTransform(GUI.Canvas.RelativeSize, backgroundBlocker.RectTransform, Anchor.Center), style: "GUIBackgroundBlocker");

            var mainParent = new GUIFrame(new RectTransform(new Vector2(0.5f, 0.4f), backgroundBlocker.RectTransform, Anchor.Center, scaleBasis: ScaleBasis.Smallest) { MinSize = new Point(640, 480) }).RectTransform;

            mainFrame = new GUIFrame(new RectTransform(Vector2.One, mainParent));
            var mainLayout = new GUILayoutGroup(new RectTransform(Vector2.One * 0.95f, mainFrame.RectTransform, Anchor.Center, Pivot.Center),
                                isHorizontal: false, childAnchor: Anchor.TopRight);

            _ = new GUITextBlock(new RectTransform((1.0f, 0.07f), mainLayout.RectTransform), TextManager.GetWithVariable("mlc.patchnote", "[version]", Main.Version),
                    font: GUIStyle.LargeFont);

            // Padding
            _ = new GUIFrame(new RectTransform((0.01f, 0.01f), mainLayout.RectTransform), style: null);

            contentFrame = new GUIFrame(new RectTransform((1.0f, 0.8f), mainLayout.RectTransform),
                style: "InnerFrame");

            _ = new GUITextBlock(new RectTransform((1.0f, 1.0f), contentFrame.RectTransform), TextManager.Get("mlc.patchnotes").Value ?? "hot spicy meme action", textAlignment: Alignment.TopLeft);

            // Padding
            _ = new GUIFrame(new RectTransform((0.01f, 0.01f), mainLayout.RectTransform), style: null);

            bottom = new GUILayoutGroup(new RectTransform((1.0f, 0.04f), mainLayout.RectTransform), isHorizontal: true) { Stretch = true, RelativeSpacing = 0.01f };

            CreateBottomButton();
        }

        private void CreateBottomButton()
        {
            GUIButton cancelButton =
                new GUIButton(new RectTransform(new Vector2(0.5f, 0.5f), bottom.RectTransform), text: "close")
                {
                    OnClicked = (btn, obj) =>
                    {
                        Close();
                        return false;
                    }
                };
        }

        public static void Open()
        {
            Instance?.Close();
            Instance = new PatchNotes();
        }

        public void Close()
        {
            mainFrame.Parent.RemoveChild(mainFrame);
            backgroundBlocker.Parent.RemoveChild(backgroundBlocker);
            if (Instance == this) { Instance = null; }
        }
    }
}

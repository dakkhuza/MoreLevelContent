using Barotrauma.MoreLevelContent.Shared.Utils;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Text;
using MoreLevelContent;
using HarmonyLib;
using MoreLevelContent.Shared.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreLevelContent.Shared;
using OpenAL;
using System.Linq;

namespace Barotrauma.MoreLevelContent.Client.UI
{
    public class MapUI : Singleton<MapUI>
    {
        static FieldInfo zoomLevel;
        static FieldInfo tooltipField;
        static FieldInfo pendingSubInfoField;
        public override void Setup()
        {
            var drawConnection = typeof(Map).GetMethod("DrawConnection", BindingFlags.NonPublic | BindingFlags.Instance);
            zoomLevel = typeof(Map).GetField("zoom", BindingFlags.Instance | BindingFlags.NonPublic);
            tooltipField = typeof(Map).GetField("tooltip", BindingFlags.Instance | BindingFlags.NonPublic);
            pendingSubInfoField = AccessTools.Field(typeof(Map), "pendingSubInfo");
            _ = Main.Harmony.Patch(drawConnection, null, new HarmonyMethod(GetType().GetMethod(nameof(OnDrawConnection), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static SubmarineInfo.PendingSubInfo pendingSubInfo;
        private static void OnDrawConnection(SpriteBatch spriteBatch, LocationConnection connection, Rectangle viewArea, Vector2 viewOffset, Location currentDisplayLocation, Map __instance)
        {
            if (connection == null || spriteBatch == null) return;
            LevelData_MLCData data = connection.LevelData.MLC();
            Vector2? connectionStart = null;
            Vector2? connectionEnd = null;
            Vector2 rectCenter = viewArea.Center.ToVector2();
            int startIndex = connection.CrackSegments.Count > 2 ? 1 : 0;
            int endIndex = connection.CrackSegments.Count > 2 ? connection.CrackSegments.Count - 1 : connection.CrackSegments.Count;
            float zoom = (float)zoomLevel.GetValue(__instance);
            int iconCount, iconIndex = GetIconIndex(connection);
            for (int i = startIndex; i < endIndex; i++)
            {
                var segment = connection.CrackSegments[i];
                Vector2 start = rectCenter + (segment[0] + viewOffset) * zoom;
                Vector2 end = rectCenter + (segment[1] + viewOffset) * zoom;
                connectionEnd = end;
                if (!connectionStart.HasValue) { connectionStart = start; }
            }
            iconCount = GetIconCount(__instance, connection);

            if (data.HasBeaconConstruction)
            {
                LocalizedString localizedString = TextManager.GetWithVariable("mlc.beaconconsttooltip", "[requestedsupplies]", data.GetRequestedSupplies());
                DrawIcon("BeaconConst", (int)(28 * zoom), RichString.Rich(localizedString));
            }

            if (data.HasDistress)
            {
                string tooltip = "mlc.distresstooltip";
                string iconStyle = "DistressBeacon";
                if (data.DistressStepsLeft <= 3)
                {
                    tooltip = "mlc.distresstooltipfaint";
                    iconStyle = "DistressBeaconFaint";
                }

                LocalizedString localizedString = TextManager.Get(tooltip);
                DrawIcon(iconStyle, (int)(28 * zoom), RichString.Rich(localizedString));
            }

            if (data.HasLostCargo)
            {
                DrawIcon("LostCargo", (int)(28 * zoom), RichString.Rich(TextManager.Get("mlc.lostcargotooltip")));
            }

            if (data.HasBlackMarket && !Main.IsRelase)
            {
                DrawIcon("DebugBlackMarket", (int)(28 * zoom), RichString.Rich("Black Market"));
            }

            void DrawIcon(string iconStyle, int iconSize, RichString tooltipText)
            {
                Vector2 iconPos = (connectionStart.Value + connectionEnd.Value) / 2;
                Vector2 iconDiff = Vector2.Normalize(connectionEnd.Value - connectionStart.Value) * iconSize;

                iconPos += (iconDiff * -(iconCount - 1) / 2.0f) + iconDiff * iconIndex;

                var style = GUIStyle.GetComponentStyle(iconStyle);
                bool mouseOn = Vector2.DistanceSquared(iconPos, PlayerInput.MousePosition) < iconSize * iconSize && IsPreferredTooltip(iconPos, __instance);
                Sprite iconSprite = style.GetDefaultSprite();
                iconSprite.Draw(spriteBatch, iconPos, (mouseOn ? style.HoverColor : style.Color) * 0.7f,
                    scale: iconSize / iconSprite.size.X);
                if (mouseOn)
                {
                    tooltipField.SetValue(__instance, (new Rectangle((iconPos - Vector2.One * iconSize / 2).ToPoint(), new Point(iconSize)), tooltipText));
                }
                iconIndex++;
            }

            bool IsPreferredTooltip(Vector2 tooltipPos, Map map) => tooltipField.GetValue(map) == null || Vector2.DistanceSquared(tooltipPos, PlayerInput.MousePosition) < Vector2.DistanceSquared((tooltipField.GetValue(map) as (Rectangle targetArea, RichString tip)?).Value.targetArea.Center.ToVector2(), PlayerInput.MousePosition);

            int GetIconCount(Map __instance, LocationConnection connection)
            {
                int iconCount = 0;
                float subCrushDepth = SubmarineInfo.GetSubCrushDepth(SubmarineSelection.CurrentOrPendingSubmarine(), ref pendingSubInfo);
                if (connection.LevelData.InitialDepth * Physics.DisplayToRealWorldRatio > subCrushDepth)
                {
                    iconIndex++;
                    iconCount++;
                }
                else if ((connection.LevelData.InitialDepth + connection.LevelData.Size.Y) * Physics.DisplayToRealWorldRatio > subCrushDepth)
                {
                    iconIndex++;
                    iconCount++;
                }

                return iconCount;
            }

            int GetIconIndex(LocationConnection connection)
            {
                int index = 0;
                if (connection.LevelData.HasBeaconStation) index++;
                if (connection.Locked) index++;
                if (connection.LevelData.HasHuntingGrounds) index++;

                return index;
            }
        }
    }
}

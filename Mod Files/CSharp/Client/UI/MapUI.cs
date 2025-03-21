﻿using Barotrauma.MoreLevelContent.Shared.Utils;
using System.Reflection;
using MoreLevelContent;
using HarmonyLib;
using MoreLevelContent.Shared.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Generation;
using System.Threading;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using Barotrauma.MoreLevelContent.Config;

namespace Barotrauma.MoreLevelContent.Client.UI
{
    public class MapUI : Singleton<MapUI>
    {
        static FieldInfo zoomLevel;
        static FieldInfo tooltipField;
        static FieldInfo pendingSubInfoField;
        static MethodInfo isInFogOfWar;
        static bool _DrawingConnections = false;


        public override void Setup()
        {
            var drawConnection = typeof(Map).GetMethod("DrawConnection", BindingFlags.NonPublic | BindingFlags.Instance);
            var draw = AccessTools.Method(typeof(Map), nameof(Map.Draw));

            zoomLevel = typeof(Map).GetField("zoom", BindingFlags.Instance | BindingFlags.NonPublic);
            tooltipField = typeof(Map).GetField("tooltip", BindingFlags.Instance | BindingFlags.NonPublic);
            pendingSubInfoField = AccessTools.Field(typeof(Map), "pendingSubInfo");
            isInFogOfWar = AccessTools.Method(typeof(Map), "IsInFogOfWar");

            _ = Main.Harmony.Patch(draw, transpiler: new HarmonyMethod(AccessTools.Method(typeof(MapUI), nameof(TranspileMapDraw))));
            _ = Main.Harmony.Patch(drawConnection, postfix: new HarmonyMethod(GetType().GetMethod(nameof(OnDrawConnection), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static SubmarineInfo.PendingSubInfo pendingSubInfo;

        private static IEnumerable<CodeInstruction> TranspileMapDraw(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Log.Debug("Transpiling map draw...");
            bool finished = false;
            var code = new List<CodeInstruction>(instructions);
            for (int i = 0; i < code.Count; i++)
            {
                if (finished == false && code[i].opcode == OpCodes.Stloc_S && code[i].operand.ToString() == "Barotrauma.LocationConnection (32)")
                {                    
                    finished = true;
                    yield return code[i];
                    yield return new CodeInstruction(OpCodes.Ldloc_S, code[i].operand); // Location connection
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // Map
                    yield return new CodeInstruction(OpCodes.Ldarg_2); // Sprite batch
                    yield return new CodeInstruction(OpCodes.Ldloc_1); // View area
                    yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)4); // View Offset
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapUI), nameof(DrawRevealedFeatures)));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, code[i].operand);
                }
                yield return code[i];
            }
            if (!finished) Log.Error("Failed to find map transpile injection point!");
        }

        private static void DrawRevealedFeatures(LocationConnection connection, Map map, SpriteBatch spriteBatch, Rectangle viewArea, Vector2 viewOffset)
        {
            // Skip if we don't have a feature or pirate base
            if (!CheckValid()) return;

            // Both sides are in fog of war
            bool inFow = (bool)isInFogOfWar.Invoke(map, new object[] { connection.Locations[0] }) && (bool)isInFogOfWar.Invoke(map, new object[] { connection.Locations[1] });
            if (inFow)
            {
                DrawCustomConnections(spriteBatch, connection, viewArea, viewOffset, map, true);
                Log.Debug("Drew custom connection");
            }

            bool CheckValid()
            {
                var feature = connection.LevelData.MLC().MapFeatureData;
                var pirateBase = connection.LevelData.MLC().PirateData;


                if (pirateBase.HasPirateBase && pirateBase.Revealed) return true;
                // Not valid if we don't have a feature
                if (!feature.HasFeature) return false;
                // Not valid if the feature isn't revealed
                if (!feature.Revealed) return false;
                // Not valid if the feature starts revealed
                if (!feature.Feature.Display.HideUntilRevealed) return false;

                return true;
            }
        }

        private static void OnDrawConnection(SpriteBatch spriteBatch, LocationConnection connection, Rectangle viewArea, Vector2 viewOffset, Map __instance, bool __state)
        {
            if (__state) return;
            DrawCustomConnections(spriteBatch, connection, viewArea, viewOffset, __instance, false);
        }

        private static void DrawCustomConnections(SpriteBatch spriteBatch, LocationConnection connection, Rectangle viewArea, Vector2 viewOffset, Map __instance, bool drawInFOW)
        {
            _DrawingConnections = false;
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

            if (drawInFOW)
            {
                iconCount = 0;
                DrawMapFeature(data);
                DrawPirateBase();
                return;
            }

            if (data.HasBeaconConstruction && ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.EnableConstructionSites)
            {
                LocalizedString localizedString = TextManager.GetWithVariable("mlc.beaconconsttooltip", "[requestedsupplies]", data.GetRequestedSupplies());
                DrawIcon("BeaconConst", (int)(28 * zoom), RichString.Rich(localizedString));
            }

            if (data.HasDistress && ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.EnableDistressMissions)
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

            if (data.HasRelayStation && ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.EnableRelayStations)
            {
                var iconName = data.RelayStationStatus == RelayStationStatus.Active ? "RelayStationActive" : "RelayStationInactive";
                var locString = data.RelayStationStatus == RelayStationStatus.Active ? "mlc.relaystationtooltip.active" : "mlc.relaystationtooltip.inactive";
                LocalizedString localizedString = TextManager.Get(locString);
                DrawIcon(iconName, (int)(28 * zoom), RichString.Rich(localizedString));
            }

            DrawPirateBase();
            DrawMapFeature(data);

            void DrawMapFeature(LevelData_MLCData data)
            {
                if (!ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.EnableMapFeatures) return;
                if (data.MapFeatureData.Name.IsEmpty) return;
                if (!data.MapFeatureData.Revealed && !GameMain.DebugDraw && !Commands.DisplayAllMapLocations) return;
                if (!MapFeatureModule.TryGetFeature(data.MapFeatureData.Name, out MapFeature feature))
                {
                    Log.Error($"Failed to find map feature with identifier {data.MapFeatureData.Name}!!");
                    return;
                }
                var tooltip = TextManager.Get(feature.Display.Tooltip);
                if (GameMain.DebugDraw)
                {
                    tooltip = $"{tooltip.Value} + {data.MapFeatureData.Revealed}";
                }
                DrawIcon(feature.Display.Icon, (int)(28 * zoom), RichString.Rich(tooltip));
            }

            void DrawPirateBase()
            {
                if (ConfigManager.Instance.Config.NetworkedConfig.PirateConfig.EnablePirateBases && 
                    data.PirateData.HasPirateBase && 
                    (GameMain.DebugDraw || Commands.DisplayAllMapLocations || data.PirateData.Revealed)) 
                { } else { return; }
                LocalizedString text = "";
                switch (data.PirateData.Status)
                {
                    case PirateOutpostStatus.Active:
                        text = TextManager.Get("piratebase.active");
                        break;
                    case PirateOutpostStatus.Destroyed:
                        text = TextManager.Get("piratebase.destroyed");
                        break;
                    case PirateOutpostStatus.Husked:
                        text = TextManager.Get("piratebase.husked");
                        break;
                }

                if (GameMain.DebugDraw)
                {
                    text += $" Revealed: {data.PirateData.Revealed}";
                }


                DrawIcon(data.PirateData.Status == PirateOutpostStatus.Active ? "PirateBase" : "PirateBaseDestroyed", (int)(28 * zoom), RichString.Rich(text));
            }

            void DrawIcon(string iconStyle, int iconSize, RichString tooltipText)
            {
                Vector2 iconPos = (connectionStart.Value + connectionEnd.Value) / 2;
                Vector2 iconDiff = Vector2.Normalize(connectionEnd.Value - connectionStart.Value) * iconSize;

                iconPos += (iconDiff * -(iconCount - 1) / 2.0f) + iconDiff * iconIndex;

                var style = GUIStyle.GetComponentStyle(iconStyle);
                if (style == null)
                {
                    Log.Error($"Unable to find icon style {style}");
                    return;
                }
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

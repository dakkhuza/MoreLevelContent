using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using static Barotrauma.Level;

namespace MoreLevelContent.Shared.Generation
{
    public static class MoveRuins
    {
        public static void Init()
        {
            if (GameMain.IsMultiplayer) return;
            var level_FindPosAwayFromMainPath = typeof(Level).GetMethod("FindPosAwayFromMainPath", BindingFlags.NonPublic | BindingFlags.Instance);
            Main.HookMethod("MLC::MoveRuinSpawnPos", level_FindPosAwayFromMainPath, MoveRuinSpawnPos, LuaCsHook.HookMethodType.Before);
        }

        readonly static Point ruinSize = new Point(5000);

        public static object MoveRuinSpawnPos(object self, Dictionary<string, object> args)
        {
            // Broken in multiplayer
            if (GameMain.IsMultiplayer) return null;
            // Exit if caves haven't been generated yet
            if (Loaded.Caves.Count < Loaded.GenerationParams.CaveCount) return null;
            Random rand = new MTRandom(ToolBox.StringToInt(Loaded.Seed));
            // Roll for move
            if (rand.Next(100) > ConfigManager.Instance.Config.NetworkedConfig.GeneralConfig.RuinMoveChance) return null;
            Log.Debug("Moving the ruins...");

            // Generate ruin point

            int limitLeft = Math.Max((int)Loaded.StartPosition.X, ruinSize.X / 2);
            int limitRight = Math.Min((int)Loaded.EndPosition.X, Loaded.Size.X - (ruinSize.X / 2));

            Point ruinPos = new Point(rand.Next(limitLeft, limitRight), rand.Next(Loaded.AbyssArea.Top + 5000, Loaded.AbyssArea.Bottom - 5000));

            // Move the ruins above the sea floor, copied from Level.cs Line 1709
            ruinPos.Y = Math.Max(ruinPos.Y, (int)Loaded.GetBottomPosition(ruinPos.X).Y + 500);
            ruinPos.Y = Math.Max(ruinPos.Y, (int)Loaded.GetBottomPosition(ruinPos.X + 5000).Y + 500);

            Log.Debug($"Ruins spawn point: {ruinPos}");

            return ruinPos;
        }
    }
}

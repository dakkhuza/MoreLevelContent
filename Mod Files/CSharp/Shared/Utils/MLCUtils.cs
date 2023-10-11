using Barotrauma;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Voronoi2;

namespace MoreLevelContent.Shared.Utils
{
    public static class MLCUtils
    {
        internal static Vector2 PositionItemOnEdge(Item target, GraphEdge edge, float height, bool setRotation = false)
        {
            Vector2 dir = Vector2.Normalize(edge.GetNormal(edge.Cell1 ?? edge.Cell2));
            float angle = Angle(dir) - 90;
            Vector2 pos = ConvertUnits.ToSimUnits(edge.Center + (edge.GetNormal(edge.Cell1 ?? edge.Cell2) * height));
            SetItemPosition(target, pos, setRotation ? MathHelper.ToRadians(angle) : 0);
            target.Rotation = -angle;
            return dir;
        }
        internal static void SetItemPosition(Item target, Vector2 simPos, float rot) => target.SetTransform(simPos - (target.Submarine?.SimPosition ?? Vector2.Zero), rot, false);
        internal static float Angle(Vector2 dir) => (float)(MathUtils.VectorToAngle(dir) * 180 / Math.PI);

        internal static Random GetLevelRandom()
        {
            if (Level.Loaded == null)
            {
                Log.Error("Level was null when we tried to get a random instance!");
                return null;
            }

            return new MTRandom(ToolBox.StringToInt(Level.Loaded.LevelData.Seed));
        }
    }
}

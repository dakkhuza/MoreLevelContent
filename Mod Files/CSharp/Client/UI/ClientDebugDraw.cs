﻿using Barotrauma;
using Microsoft.Xna.Framework.Graphics;
using MoreLevelContent.Shared.Generation;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Utils;

public static class ClientDebugDraw
{
    internal static void Draw(SpriteBatch spriteBatch, Camera cam)
    {
        foreach (var item in CaveGenerationDirector.Instance._InitialCaveCheckDebug)
        {
            GUI.DrawString(spriteBatch, new Vector2(item.Cell.Center.X, -item.Cell.Center.Y), "Cell", Color.Azure);
        }
        if (TurretExtensions.TargetHull != default)
        {
            GUI.DrawRectangle(spriteBatch,
                TurretExtensions.TargetHull, 
                Color.Yellow, thickness: 2);
        }
        GUI.DrawRectangle(spriteBatch, TurretExtensions.Hit, 20, 20, 0, Color.Orange, thickness: 10);
        if (CaveGenerationDirector.Instance.ActiveThalaCave != null) CaveGenerationDirector.Instance.ActiveThalaCave.DebugDraw(spriteBatch, cam);
    }
}
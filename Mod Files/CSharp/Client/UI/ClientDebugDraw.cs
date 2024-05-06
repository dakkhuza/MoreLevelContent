using Barotrauma;
using Microsoft.Xna.Framework.Graphics;
using MoreLevelContent.Shared.Generation;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Utils;
using Barotrauma.Items.Components;
using MoreLevelContent.Shared;
using System;

public static class ClientDebugDraw
{
    internal static void Draw(SpriteBatch spriteBatch, Camera cam)
    {
        if (Level.Loaded != null)
        {
            // spriteBatch.DrawCircle(new Vector2(Level.Loaded.StartPosition.X, -Level.Loaded.StartPosition.Y), CaveGenerationDirector.MIN_DIST_FROM_START, 16, Color.Red, thickness: 100);
            // spriteBatch.DrawLine(new Vector2(0, -Level.Loaded.StartPosition.Y + (Sonar.DefaultSonarRange / 2)), new Vector2(int.MaxValue, -Level.Loaded.StartPosition.Y + (Sonar.DefaultSonarRange / 2)), Color.Yellow, thickness: 2 / Screen.Selected.Cam.Zoom * GUI.Scale);
        }

        foreach (var item in CaveGenerationDirector.Instance._InitialCaveCheckDebug)
        {
            GUI.DrawString(spriteBatch, new Vector2(item.Cell.Center.X, -item.Cell.Center.Y), "Cell", Color.Azure);
        }
        if (CaveGenerationDirector.Instance.ActiveThalaCave != null) CaveGenerationDirector.Instance.ActiveThalaCave.DebugDraw(spriteBatch, cam);
    }
}

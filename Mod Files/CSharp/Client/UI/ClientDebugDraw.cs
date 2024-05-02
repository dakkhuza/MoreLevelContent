using Barotrauma;
using Microsoft.Xna.Framework.Graphics;
using MoreLevelContent.Shared.Generation;
using Microsoft.Xna.Framework;

public static class ClientDebugDraw
{
    internal static void Draw(SpriteBatch spriteBatch, Camera cam)
    {
        foreach (var item in CaveGenerationDirector.Instance._InitialCaveCheckDebug)
        {
            GUI.DrawString(spriteBatch, new Vector2(item.Cell.Center.X, -item.Cell.Center.Y), "Cell", Color.Azure);
        }
        if (CaveGenerationDirector.Instance.ActiveThalaCave != null) CaveGenerationDirector.Instance.ActiveThalaCave.DebugDraw(spriteBatch, cam);
    }
}

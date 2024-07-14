using Barotrauma.Extensions;
using Barotrauma.Networking;
using Barotrauma;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using MoreLevelContent.Shared.Utils;
using System.Reflection.Metadata;
using MoreLevelContent.Shared.Generation;

namespace MoreLevelContent.Shared.AI
{
    partial class CaveAI
    {
        private CoroutineHandle fadeOutRoutine;
        partial void FadeOutColors()
        {
            if (fadeOutRoutine != null)
            {
                CoroutineManager.StopCoroutines(fadeOutRoutine);
            }
            fadeOutRoutine = CoroutineManager.StartCoroutine(FadeOutColors(Config.DeadEntityColorFadeOutTime));
        }

        private IEnumerable<CoroutineStatus> FadeOutColors(float time)
        {
            float timer = 0;
            while (timer < time)
            {
                timer += CoroutineManager.DeltaTime;
                float m = MathHelper.Lerp(1, Config.DeadEntityColorMultiplier, MathUtils.InverseLerp(0, time, timer));
                foreach (var item in ThalamusItems)
                {
                    if (item.Prefab.BrokenSprites.None())
                    {
                        Color c = item.Prefab.SpriteColor;
                        item.SpriteColor = new Color(c.R / 255f * m, c.G / 255f * m, c.B / 255f * m, c.A / 255f);
                    }
                }
                yield return CoroutineStatus.Running;
            }
            yield return CoroutineStatus.Success;
        }

        public void DebugDraw(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, Camera cam)
        {
            float lineThickness = 1f / Screen.Selected.Cam.Zoom;
            sb.DrawPoint(new Vector2(Cave.StartPos.X, -Cave.StartPos.Y), Color.Pink, 10 / Screen.Selected.Cam.Zoom);
            foreach (var turret in turrets)
            {
                const float coneRadius = 300.0f;
                float radians = turret.GetMaxRotation() - turret.GetMinRotation();
                float circleRadius = coneRadius / Screen.Selected.Cam.Zoom * GUI.Scale;
              
                sb.DrawSector(turret.GetDrawPos(), circleRadius, radians, (int)Math.Abs(90 * radians), GUIStyle.Green, offset: turret.GetMinRotation(), thickness: lineThickness);

                Dictionary<ActionType, List<StatusEffect>> dic = (Dictionary<ActionType, List<StatusEffect>>)CaveGenerationDirector.item_statusEffectList.GetValue(turret.Item);
                if (dic?.TryGetValue(ActionType.OnUse, out List<StatusEffect> effects) ?? false)
                {
                    foreach (var effect in effects)
                    {
                        var pos = turret.Item.Position + new Vector2(effect.Offset.X, effect.Offset.Y);
                        pos = new Vector2(pos.X, -pos.Y);
                        GUI.DrawRectangle(sb, pos, 100, 100, 0, Color.Aqua, thickness: lineThickness);
                        foreach (var spawnEffect in effect.SpawnCharacters)
                        {
                            var pos2 = turret.Item.Position + new Vector2(spawnEffect.Offset.X, spawnEffect.Offset.Y);
                            pos2 = new Vector2(pos2.X, -pos2.Y);
                            GUI.DrawRectangle(sb, pos2, 50, 50, 0, Color.Orange, thickness: lineThickness);
                        }
                    }
                }

            }
        }

        public void ClientEventRead(IReadMessage msg, float sendingTime)
        {
            IsAlive = msg.ReadBoolean();
        }
    }
}

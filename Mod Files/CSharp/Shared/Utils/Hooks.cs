using Barotrauma;
using HarmonyLib;
using System.Reflection;
using System;
using Barotrauma.MoreLevelContent.Shared.Utils;
using Barotrauma.Items.Components;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.Xna.Framework;

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

namespace MoreLevelContent.Shared.Utils
{
    internal class Hooks : Singleton<Hooks>
    {
        internal event Action<Structure, float, Character> OnStructureDamaged;
#if CLIENT
        internal event Action<SpriteBatch, Camera> OnDebugDraw;
        internal event Action<Sonar, Vector2, float> OnUpdateSonarDisruption;
        private FieldInfo Sonar_activePingsCount;
#endif

        public override void Setup()
        {
            var Structure_SetDamage = typeof(HumanAIController).GetMethod(nameof(HumanAIController.StructureDamaged), BindingFlags.Public | BindingFlags.Static);
            _ = Main.Harmony.Patch(Structure_SetDamage, prefix: new HarmonyMethod(typeof(Hooks), nameof(StructureDamaged)));

#if CLIENT
            var Level_DrawDebugOverlay = typeof(Level).GetMethod(nameof(Level.DrawDebugOverlay));
            _ = Main.Harmony.Patch(Level_DrawDebugOverlay, postfix: new HarmonyMethod(typeof(Hooks), nameof(DebugDraw)));

            var Sonar_UpdateDisruptions = AccessTools.Method(typeof(Sonar), "UpdateDisruptions");//typeof(Sonar).GetMethod("UpdateDisruptions");
            Sonar_activePingsCount = AccessTools.Field(typeof(Sonar), "activePingsCount");
            if (Sonar_UpdateDisruptions == null) Log.Error("\n\n\n\nNull");
            _ = Main.Harmony.Patch(Sonar_UpdateDisruptions, postfix: new HarmonyMethod(typeof(Hooks), nameof(SonarDisruption)));
#endif
            Log.Debug("Patched Hooks");
        }

        private static void StructureDamaged(Structure structure, float damageAmount, Character character) => Instance.OnStructureDamaged?.Invoke(structure, damageAmount, character);

#if CLIENT
        private static void DebugDraw(SpriteBatch spriteBatch, Camera cam)
        {
            if (!GameMain.DebugDraw) return;
            Instance.OnDebugDraw?.Invoke(spriteBatch, cam);
        }

        private static void SonarDisruption(Sonar __instance, Vector2 pingSource, float worldPingRadius)
        {
            int activePingsCount = (int)Instance.Sonar_activePingsCount.GetValue(__instance);
            for (var pingIndex = 0; pingIndex < activePingsCount; ++pingIndex)
            {
                Instance.OnUpdateSonarDisruption?.Invoke(__instance, pingSource, worldPingRadius);
            }
        }
#endif
    }
}

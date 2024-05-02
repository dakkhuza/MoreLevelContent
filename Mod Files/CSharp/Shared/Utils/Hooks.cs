using Barotrauma;
using HarmonyLib;
using System.Reflection;
using System;
using Barotrauma.MoreLevelContent.Shared.Utils;

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
#endif

        public override void Setup()
        {
            var Structure_SetDamage = typeof(HumanAIController).GetMethod(nameof(HumanAIController.StructureDamaged), BindingFlags.Public | BindingFlags.Static);
            _ = Main.Harmony.Patch(Structure_SetDamage, prefix: new HarmonyMethod(typeof(Hooks), nameof(StructureDamaged)));
#if CLIENT
            var Level_DrawDebugOverlay = typeof(Level).GetMethod(nameof(Level.DrawDebugOverlay));
            _ = Main.Harmony.Patch(Level_DrawDebugOverlay, postfix: new HarmonyMethod(typeof(Hooks), nameof(DebugDraw)));
#endif
            Log.Error("Patched Hooks");
        }

        private static void StructureDamaged(Structure structure, float damageAmount, Character character) => Instance.OnStructureDamaged?.Invoke(structure, damageAmount, character);

#if CLIENT
        private static void DebugDraw(SpriteBatch spriteBatch, Camera cam) => Instance.OnDebugDraw?.Invoke(spriteBatch, cam);
#endif
    }
}

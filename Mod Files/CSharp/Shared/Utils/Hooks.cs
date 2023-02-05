using Barotrauma;
using HarmonyLib;
using System.Reflection;
using System;
using Barotrauma.MoreLevelContent.Shared.Utils;

namespace MoreLevelContent.Shared.Utils
{
    internal class Hooks : Singleton<Hooks>
    {
        internal event Action<Structure, float, Character> OnStructureDamaged;
        public override void Setup()
        {
            var Structure_SetDamage = typeof(HumanAIController).GetMethod(nameof(HumanAIController.StructureDamaged), BindingFlags.Public | BindingFlags.Static);
            _ = Main.Harmony.Patch(Structure_SetDamage, prefix: new HarmonyMethod(typeof(Hooks), nameof(StructureDamaged)));
            Log.Error("Patched Hooks");
        }

        private static void StructureDamaged(Structure structure, float damageAmount, Character character) => Instance.OnStructureDamaged?.Invoke(structure, damageAmount, character);
    }
}

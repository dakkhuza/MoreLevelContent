using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using System.Reflection;

namespace MoreLevelContent.Shared.AI
{
    public class MLCAIObjectiveManager : Singleton<MLCAIObjectiveManager>
    {
        public override void Setup()
        {
            MethodInfo info = AccessTools.Method(typeof(AIObjectiveManager), nameof(AIObjectiveManager.CreateObjective));
            Main.Patch(info, postfix: new HarmonyMethod(typeof(MLCAIObjectiveManager), nameof(MLCAIObjectiveManager.AIObjectiveManager_CreateObjective)));
            Log.Debug("Setup AI override");
        }


        /*
         * 
         * 
Exception: Object reference not set to an instance of an object. (System.NullReferenceException)
Target site: Void AIObjectiveManager_CreateObjective(Barotrauma.AIObjective ByRef, Barotrauma.AIObjectiveManager, Barotrauma.Character, Barotrauma.Order)
Stack trace: 
   at MoreLevelContent.Shared.AI.MLCAIObjectiveManager.AIObjectiveManager_CreateObjective(AIObjective& __result, AIObjectiveManager __instance, Character ___character, Order order)
   at Barotrauma.AIObjectiveManager.CreateObjective_Patch1
         * 
         * 
         * 
         */

        internal static void AIObjectiveManager_CreateObjective(ref AIObjective __result, AIObjectiveManager __instance, Character ___character, Order order, float priorityModifier)
        {
            if (order == null || order.IsDismissal) { return; }
            Log.Debug("Yoinky");
            AIObjective newObjective;
            switch (order.Identifier.Value.ToLowerInvariant())
            {
                case "traitorinjectitem":
                    newObjective = new AITraitorObjectiveInjectItem(___character, __instance, priorityModifier, order.Option, order.GetTargetItems(order.Option));
                    Log.Debug("Overrode objective");
                    break;
                case "fightintrudersanysub":
                    newObjective = new AIFightIntrudersAnySubObjective(___character, __instance, priorityModifier);
                    Log.Debug("Overrode objective");
                    break;
                default:
                    return;
            }
            if (newObjective != null)
            {
                newObjective.Identifier = order.Identifier;
            }
            __result = newObjective;
            Log.Debug("da returny");
        }
    }
}

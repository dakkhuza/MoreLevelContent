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
        }

        internal static void AIObjectiveManager_CreateObjective(ref AIObjective __result, AIObjectiveManager __instance, Character ___character, Order order)
        {
            if (order == null || order.IsDismissal) { return; }

            AIObjective newObjective;
            switch (order.Identifier.Value.ToLowerInvariant())
            {
                case "traitorinjectitem":
                    newObjective = new AITraitorObjectiveInjectItem(___character, __instance, 1, order.Option, order.GetTargetItems(order.Option));
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
        }
    }
}

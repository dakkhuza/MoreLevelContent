using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using MoreLevelContent.Custom.Missions;
using MoreLevelContent.Shared.Content;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoreLevelContent.Shared.XML
{
    public class InjectionManager : Singleton<InjectionManager>
    {
        public override void Setup()
        {
            missionPrefab_constructor = AccessTools.Field(typeof(MissionPrefab), "constructor");
            npcConversation_GetCurrentFlags = AccessTools.Method(typeof(NPCConversation), "GetCurrentFlags");
            _ = Main.Harmony.Patch(npcConversation_GetCurrentFlags, postfix: new HarmonyMethod(AccessTools.Method(typeof(InjectionManager), nameof(AddNPCConcersationFlags))));
            InjectMissions();
        }
        
        public void Cleanup()
        {
            // Cleanup missions
            // foreach (Identifier identifier in missionIdentifiers)
            // {
            //     Log.Debug($"Cleaned up mission with identifier {identifier.Value}");
            // }
        }

        private static void AddNPCConcersationFlags(ref List<Identifier> __result, Character speaker)
        {
            if (speaker == null) return;
            if (speaker.MLC().IsDistressShuttle) __result.Add("DistressShuttle");
            if (speaker.MLC().IsDistressDiver) __result.Add("DistressDiver");

            if (speaker.TeamID == CharacterTeamType.FriendlyNPC)
            {
                if (speaker.Submarine == MapFeatureModule.MapFeatureSub)
                {
                    __result.Add(MapFeatureModule.CurrentMapFeature);
                }
                if (Submarine.MainSub != null && speaker.Submarine == Submarine.MainSub)
                {
                    __result.Add("MainSub");
                }
            }

        }

        FieldInfo missionPrefab_constructor;
        MethodInfo npcConversation_GetCurrentFlags;

        private void InjectMissions()
        {
            Log.Debug("Injecting custom missions");
            foreach (MissionPrefab prefab in MissionPrefab.Prefabs)
            {
                Identifier customType = prefab.ConfigElement.GetAttributeIdentifier("customType", Identifier.Empty);
                if(!Enum.TryParse(customType.Value, true, out CustomMissionType type)) continue;
                missionPrefab_constructor.SetValue(prefab, CustomMissions.MissionDefs[type].GetConstructor(new[] { typeof(MissionPrefab), typeof(Location[]), typeof(Submarine) }));
                Log.Verbose($"Updated consturctor for mission {prefab.Name} to type {type}");
            }
        }
    }
}

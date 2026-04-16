using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using MoreLevelContent.Custom.Missions;
using MoreLevelContent.Shared.Content;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace MoreLevelContent.Shared.XML
{
    [AttributeUsage(AttributeTargets.Class)]
    public class InjectScriptedEvent : Attribute { }

    public class InjectionManager : Singleton<InjectionManager>
    {
        FieldInfo missionPrefab_constructor;
        static ImmutableDictionary<string, Type> _CustomScriptedEvents;
        private static Assembly _BaroAsm;

        public override void Setup()
        {
            missionPrefab_constructor = AccessTools.Field(typeof(MissionPrefab), "constructor");
            var npcConversation_GetCurrentFlags = AccessTools.Method(typeof(NPCConversation), "GetCurrentFlags");
            var eventAction_Instantiate = AccessTools.Method(typeof(EventAction), "Instantiate");
            _BaroAsm = Assembly.GetAssembly(typeof(EventAction));

            _ = Main.Harmony.Patch(npcConversation_GetCurrentFlags, postfix: new HarmonyMethod(AccessTools.Method(typeof(InjectionManager), nameof(AddNPCConcersationFlags))));
            _ = Main.Harmony.Patch(eventAction_Instantiate, prefix: new HarmonyMethod(AccessTools.Method(typeof(InjectionManager), nameof(InjectEventActions))));
            
            // Collect scripted events
            Dictionary<string, Type> customEventDict = new()
            {
                { nameof(AlterMapFeatureAction), typeof(AlterMapFeatureAction) },
                { nameof(RevealMapAreaAction), typeof(RevealMapAreaAction) },
                { nameof(RevealMapFeatureAction), typeof(RevealMapFeatureAction) },
                { nameof(RevealPirateBaseAction), typeof(RevealPirateBaseAction) },
                { nameof(TeleportCharacterAction), typeof(TeleportCharacterAction) }
            };

            _CustomScriptedEvents = customEventDict.ToImmutableDictionary();
            
            
            
            InjectMissions();
        }
        
        // This should be a transpiler but I don't want to spend the time doing that and I don't think any other mod is ever going to add custom scripted events
        // We'll fix this incompatability when it happens!
        private static bool InjectEventActions(ScriptedEvent scriptedEvent, ContentXElement element, ref EventAction __result)
        {
            Type actionType;
            __result = null;
            try
            {
                Identifier typeName = element.Name.ToString().ToIdentifier();
                if (typeName == "TutorialSegmentAction")
                {
                    typeName = nameof(EventObjectiveAction).ToIdentifier();
                }
                else if (typeName == "TutorialHighlightAction")
                {
                    typeName = nameof(HighlightAction).ToIdentifier();
                }

                if (!_CustomScriptedEvents.TryGetValue(typeName.ToString(), out actionType))
                {
                    actionType = _BaroAsm.GetType("Barotrauma." + typeName, throwOnError: true, ignoreCase: true);
                    //actionType = _BaroAsm.GetType("Barotrauma." + typeName.ToString(), throwOnError: true, ignoreCase: true);
                }

                if (actionType == null) { throw new NullReferenceException(); }
            }
            catch(Exception e)
            {
                Log.Error(e.Message);
                DebugConsole.ThrowError($"Could not find an {nameof(EventAction)} class of the type \"{element.Name}\".",
                    contentPackage: element.ContentPackage);
                return false;
            }

            ConstructorInfo constructor = actionType.GetConstructor(new[] { typeof(ScriptedEvent), typeof(ContentXElement) });
            try
            {
                if (constructor == null)
                {
                    throw new Exception($"Error in scripted event \"{scriptedEvent.Prefab.Identifier}\" - could not find a constructor for the EventAction \"{actionType}\".");
                }
                __result = constructor.Invoke(new object[] { scriptedEvent, element }) as EventAction;
                return false;
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError(ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString(),
                    contentPackage: element.ContentPackage);
                return false;
            }
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

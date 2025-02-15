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

        public override void Setup()
        {
            missionPrefab_constructor = AccessTools.Field(typeof(MissionPrefab), "constructor");
            var npcConversation_GetCurrentFlags = AccessTools.Method(typeof(NPCConversation), "GetCurrentFlags");
            var eventAction_Instantiate = AccessTools.Method(typeof(EventAction), "Instantiate");
            _ = Main.Harmony.Patch(npcConversation_GetCurrentFlags, postfix: new HarmonyMethod(AccessTools.Method(typeof(InjectionManager), nameof(AddNPCConcersationFlags))));
            _ = Main.Harmony.Patch(eventAction_Instantiate, prefix: new HarmonyMethod(AccessTools.Method(typeof(InjectionManager), nameof(InjectEventActions))));

            // Collect scripted events
            Dictionary<string, Type> customEventDict = new();
            foreach (Type type in Assembly.GetCallingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute(typeof(InjectScriptedEvent), true) is InjectScriptedEvent injectScriptedEvent)
                {
                    customEventDict.Add(type.Name, type);
                    Log.Debug($"Registered custom scripted event {type.Name}");
                }
            }
            _CustomScriptedEvents = customEventDict.ToImmutableDictionary();



            InjectMissions();
        }
        
        // This should be a transpiler but I don't want to spend the time doing that and I don't think any other mod is ever going to add custom scripted events
        // We'll fix this incompatability when it happens!
        private static bool InjectEventActions(ScriptedEvent scriptedEvent, ContentXElement element, ref EventAction __result)
        {
            string typeName = element.Name.ToString();
            if (!_CustomScriptedEvents.TryGetValue(typeName, out Type actionType)) return true;
            if (actionType != null)
            {
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
            return true;
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

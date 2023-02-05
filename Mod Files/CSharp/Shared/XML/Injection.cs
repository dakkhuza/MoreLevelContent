using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using MoreLevelContent.Custom.Missions;
using MoreLevelContent.Shared.Content;
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

        FieldInfo missionPrefab_constructor;

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

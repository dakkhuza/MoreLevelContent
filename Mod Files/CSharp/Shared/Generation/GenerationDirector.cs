using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static Barotrauma.Level;
using static HarmonyLib.Code;

namespace MoreLevelContent.Shared.Generation
{
    public abstract class GenerationDirector<T> : Singleton<T>, IActive where T : class
    {
        static GenerationDirector()
        {
            _autofill = typeof(AutoItemPlacer).GetMethod("CreateAndPlace", BindingFlags.NonPublic | BindingFlags.Static);
            if (_autofill == null)
            {
                Log.Error("Unable to reflect");
            }
        }

        private static readonly MethodInfo _autofill;

        public abstract bool Active { get; }

        internal Submarine SpawnSubOnPath(string name, string path, bool ignoreCrushDepth = false, SubmarineType submarineType = SubmarineType.EnemySubmarine, PlacementType placementType = PlacementType.Bottom)
        {
            Submarine placedSub = SubPlacementUtils.SpawnSubOnPath(name, path, submarineType, placementType);
            if (placedSub == null)
            {
                Log.Error("SpawnSubOnPath failed to spawn wanted sub.");
                return null;
            }
            SubPlacementUtils.SetCrushDepth(placedSub, ignoreCrushDepth);
            return placedSub;
        }

        internal Submarine SpawnSubOnPath(string name, ContentFile sub, bool ignoreCrushDepth = false, SubmarineType submarineType = SubmarineType.EnemySubmarine, PlacementType placementType = PlacementType.Bottom)
        {
            Submarine placedSub = SubPlacementUtils.SpawnSubOnPath(name, sub, submarineType, placementType);
            SubPlacementUtils.SetCrushDepth(placedSub, ignoreCrushDepth);
            return placedSub;
        }
        internal void AutofillSub(Submarine sub, float skipChance = 0.5f) => _autofill.Invoke(null, new object[] { sub.ToEnumerable(), null, skipChance });
    }
}

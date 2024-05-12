using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

        internal Submarine SpawnSubOnPath(string name, string path, bool ignoreCrushDepth = false, SubmarineType submarineType = SubmarineType.EnemySubmarine)
        {
            Submarine placedSub = SubPlacementUtils.SpawnSubOnPath(name, path, submarineType);
            SubPlacementUtils.SetCrushDepth(placedSub, ignoreCrushDepth);
            return placedSub;
        }

        internal Submarine SpawnSubOnPath(string name, ContentFile sub, bool ignoreCrushDepth = false, SubmarineType submarineType = SubmarineType.EnemySubmarine)
        {
            Submarine placedSub = SubPlacementUtils.SpawnSubOnPath(name, sub, submarineType);
            SubPlacementUtils.SetCrushDepth(placedSub, ignoreCrushDepth);
            return placedSub;
        }
        internal void AutofillSub(Submarine sub) => _autofill.Invoke(null, new object[] { sub.ToEnumerable(), null, 0.0f });
    }
}

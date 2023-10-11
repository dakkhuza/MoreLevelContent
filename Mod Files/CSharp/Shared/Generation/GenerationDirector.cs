using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.Generation.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MoreLevelContent.Shared.Generation
{
    public abstract class GenerationDirector<T> : Singleton<T>, IActive where T : class
    {
        static GenerationDirector()
        {
            if (_spawnSubOnPath != null) return;
            _spawnSubOnPath = typeof(Level).GetMethod("SpawnSubOnPath", BindingFlags.NonPublic | BindingFlags.Instance);
            _autofill = typeof(AutoItemPlacer).GetMethod("CreateAndPlace", BindingFlags.NonPublic | BindingFlags.Static);
            if (_spawnSubOnPath == null || _autofill == null)
            {
                Log.Error("Unable to reflect");
            }
        }

        private static readonly MethodInfo _spawnSubOnPath;
        private static readonly MethodInfo _autofill;

        public abstract bool Active { get; }

        internal Submarine SpawnSubOnPath(Level level, string name, ContentFile sub) => _spawnSubOnPath.Invoke(level, new object[] { name, sub, SubmarineType.BeaconStation }) as Submarine;
        internal void AutofillSub(Submarine sub) => _autofill.Invoke(null, new object[] { sub.ToEnumerable(), null });
    }
}

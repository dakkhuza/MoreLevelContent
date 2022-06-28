using Barotrauma;
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
            _spawnSubOnPath = _spawnSubOnPath = typeof(Level).GetMethod("SpawnSubOnPath", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static readonly MethodInfo _spawnSubOnPath;

        public abstract bool Active { get; }

        internal Submarine SpawnSubOnPath(Level level, string name, OutpostModuleFile sub) => _spawnSubOnPath.Invoke(level, new object[] { name, sub, SubmarineType.BeaconStation }) as Submarine;
    }
}

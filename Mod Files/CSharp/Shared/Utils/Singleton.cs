using System;
using System.Collections.Generic;
using System.Text;

namespace Barotrauma.MoreLevelContent.Shared.Utils
{
    public abstract class Singleton<T> where T : class
    {
        /// <summary>
        /// Static instance. Needs to use lambda expression
        /// to construct an instance (since constructor is private).
        /// </summary>
        private static readonly Lazy<T> sInstance = new Lazy<T>(() => CreateInstanceOfT());

        /// <summary>
        /// Gets the instance of this singleton.
        /// </summary>
        public static T Instance => sInstance.Value;

        /// <summary>
        /// Creates an instance of T via reflection since T's constructor is expected to be private.
        /// </summary>
        /// <returns></returns>
        private static T CreateInstanceOfT() => Activator.CreateInstance(typeof(T), true) as T;

        public abstract void Setup();
    }
}

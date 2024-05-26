using Barotrauma;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using static Barotrauma.Items.Components.Sonar;

namespace MoreLevelContent.Shared.Utils
{
    public class SonarExtensions : Singleton<SonarExtensions>
    {
        private readonly List<SonarDisturbance> _Disturbances = new();

        public override void Setup()
        {
#if CLIENT
            Hooks.Instance.OnUpdateSonarDisruption += Instance_OnUpdateSonarDisruption;
            Dictionary<BlipType, Color[]> sonarBlips = (Dictionary<BlipType, Color[]>)ReflectionInfo.Instance.blipColorGradient.GetValue(null);
            if (!sonarBlips.ContainsKey((BlipType)5))
            {
                sonarBlips.Add((BlipType)5, new Color[] { Color.TransparentBlack, Color.TransparentBlack, new Color(0, 68, 65) * 0.8f, Color.TransparentBlack });
            }
#endif
        }

        internal void Add(Item source, float strength)
        {
            _Disturbances.Add(new SonarDisturbance(source, strength));
        }

        internal void Remove(Item source)
        {
            var dist = _Disturbances.Find(d => d.Item == source);
            if (dist != null)
            {
                _Disturbances.Remove(dist);
            }
        }

        private void Instance_OnUpdateSonarDisruption(Barotrauma.Items.Components.Sonar sonar, Vector2 pingSource, float worldPingRadius)
        {
            for (int i = 0; i < _Disturbances.Count; i++)
            {
                var disturbance = _Disturbances[i];
                if (disturbance.Item == null || disturbance.Item.Removed)
                {
                    _Disturbances.Remove(disturbance);
                    continue;
                }
                MLCExtensions.AddSonarDisruption(sonar, pingSource, disturbance.Position, disturbance.Strength);
            }
        }

        private class SonarDisturbance
        {
            public SonarDisturbance(Item source, float strength)
            {
                Item = source;
                Strength = strength;
            }
            internal Item Item { get; private set; }
            public float Strength { get; private set; }

            public Vector2 Position => Item?.WorldPosition ?? Vector2.Zero;

        }
    }
}

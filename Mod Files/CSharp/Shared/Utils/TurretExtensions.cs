using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using static Barotrauma.Items.Components.Sonar;

namespace MoreLevelContent.Shared.Utils
{
    internal static class MLCExtensions
    {
        // Private field access
        internal static float GetMinRotation(this Turret turret) => (float)ReflectionInfo.Instance.minRotation.GetValue(turret);
        internal static float GetMaxRotation(this Turret turret) => (float)ReflectionInfo.Instance.maxRotation.GetValue(turret);
        internal static List<(Vector2 pos, float strength)> GetDisruptedDirections(this Sonar sonar) => (List<(Vector2 pos, float strength)>)ReflectionInfo.Instance.disruptedDirections.GetValue(sonar);
    
        internal static void AddSonarDisruption(this Sonar sonar, Vector2 pingSource, Vector2 disruptionPos, float disruptionStrength)
        {
            disruptionStrength = Math.Min(disruptionStrength, 10.0f);
            Vector2 dir = disruptionPos - pingSource;
            float disruptionDist = Vector2.Distance(pingSource, disruptionPos);
            sonar.GetDisruptedDirections().Add(((disruptionPos - pingSource) / disruptionDist, disruptionStrength));
            for (int i = 0; i < disruptionStrength * 10.0f; i++)
            {
                Vector2 pos = disruptionPos + Rand.Vector(Rand.Range(0.0f, Level.GridCellSize * 4 * disruptionStrength));
                if (Vector2.Dot(pos - pingSource, -dir) > 1.0f - disruptionStrength) { continue; }
                var blip = new SonarBlip(
                    pos,
                    MathHelper.Lerp(0.1f, 1.5f, Math.Min(disruptionStrength, 1.0f)),
                    Rand.Range(0.2f, 1.0f + disruptionStrength),
                    BlipType.Disruption);
                List<SonarBlip> blips = (List<SonarBlip>)ReflectionInfo.Instance.sonarBlips.GetValue(sonar);
                blips.Add(blip);
            }

        }
    }

    public class ReflectionInfo : Singleton<ReflectionInfo>
    {
        public FieldInfo minRotation;
        public FieldInfo maxRotation;
        public FieldInfo disruptedDirections;
        public FieldInfo sonarBlips;
        
        public override void Setup()
        {
            // Fields
            minRotation = AccessTools.Field(typeof(Turret), "minRotation");
            maxRotation = AccessTools.Field(typeof(Turret), "maxRotation");
            disruptedDirections = AccessTools.Field(typeof(Sonar), "disruptedDirections");
            sonarBlips = AccessTools.Field(typeof(Sonar), "sonarBlips");
            Log.Debug("Setup field references");
        }
    }
}

using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Steamworks.Ugc;
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
#if CLIENT
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
#endif
        }

#if CLIENT
        // Still doesn't work
        internal static void AddSonarCircle(this Sonar sonar, Vector2 pingSource, BlipType type, int blipCount = 10, float range = 0)
        {
            Vector2 sonarPos = sonar.Item.Submarine != null && sonar.Item.body == null ? sonar.Item.Submarine.WorldPosition : sonar.Item.WorldPosition; 
            Vector2 targetVector = pingSource - sonarPos;
            if (targetVector.LengthSquared() > MathUtils.Pow2(range)) return;

            // Don't display pings if we're in sonar range
            if (targetVector.LengthSquared() < MathUtils.Pow2(DefaultSonarRange)) return;

            float dist = targetVector.Length();
            Vector2 targetDir = targetVector / dist;
            for (int i = 0; i < blipCount; i++)
            {
                float angle = Rand.Range(-0.5f, 0.5f);
                Vector2 blipDir = MathUtils.RotatePoint(targetDir, angle);
                Vector2 invBlipDir = MathUtils.RotatePoint(targetDir, -angle);
                var longRangeBlip = new SonarBlip(sonarPos + blipDir * sonar.Range * 0.9f, Rand.Range(1.9f, 2.1f), Rand.Range(1.0f, 1.5f), type)
                {
                    Velocity = -invBlipDir * (MathUtils.Round(Rand.Range(8000.0f, 15000.0f), 2000.0f) - Math.Abs(angle * angle * 10000.0f)),
                    Rotation = (float)Math.Atan2(-invBlipDir.Y, invBlipDir.X),
                    Alpha = MathUtils.Pow2((range - dist) / range) * 0.25f
                };
                longRangeBlip.Size.Y *= 4.0f;
                List<SonarBlip> blips = (List<SonarBlip>)ReflectionInfo.Instance.sonarBlips.GetValue(sonar);
                blips.Add(longRangeBlip);
            }



            // for (int i = 0; i < amount; i++)
            // {
            //     Vector2 dir = Rand.Vector(1.0f);
            //     var longRangeBlip = new SonarBlip(pingSource, Rand.Range(1.9f, 2.1f), Rand.Range(1.0f, 1.5f), type)
            //     {
            //         Velocity = dir * MathUtils.Round(Rand.Range(4000.0f, 6000.0f), 1000.0f),
            //         Rotation = (float)Math.Atan2(-dir.Y, dir.X)
            //     };
            //     longRangeBlip.Size.Y *= 4.0f;
            //     List<SonarBlip> blips = (List<SonarBlip>)ReflectionInfo.Instance.sonarBlips.GetValue(sonar);
            //     blips.Add(longRangeBlip);
            // }
        }
#endif

    }

    public class ReflectionInfo : Singleton<ReflectionInfo>
    {
        public FieldInfo minRotation;
        public FieldInfo maxRotation;
        public FieldInfo disruptedDirections;
        public FieldInfo sonarBlips;
        public FieldInfo blipColorGradient;
        
        public override void Setup()
        {
            // Fields
            minRotation = AccessTools.Field(typeof(Turret), "minRotation");
            maxRotation = AccessTools.Field(typeof(Turret), "maxRotation");
            disruptedDirections = AccessTools.Field(typeof(Sonar), "disruptedDirections");
            sonarBlips = AccessTools.Field(typeof(Sonar), "sonarBlips");
            blipColorGradient = AccessTools.Field(typeof(Sonar), "blipColorGradient");
            Log.Debug("Setup field references");
        }
    }
}

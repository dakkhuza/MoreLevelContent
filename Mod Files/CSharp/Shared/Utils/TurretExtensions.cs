using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Steamworks.Ugc;
using System;
using System.Reflection;
using System.Security.Cryptography;
using Item = Barotrauma.Item;

namespace MoreLevelContent.Shared.Utils
{
    internal static class TurretExtensions
    {
        // Private field access
        internal static float GetMinRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.minRotation.GetValue(turret);
        internal static float GetMaxRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.maxRotation.GetValue(turret);
    }

    public class TurretReflectionInfo : Singleton<TurretReflectionInfo>
    {
        public FieldInfo minRotation;
        public FieldInfo maxRotation;

        public override void Setup()
        {
            // Fields
            minRotation = AccessTools.Field(typeof(Turret), "minRotation");
            maxRotation = AccessTools.Field(typeof(Turret), "maxRotation");
            Log.Debug("Setup turret field references");
        }
    }
}

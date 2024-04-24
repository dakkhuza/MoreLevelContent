using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.MoreLevelContent.Shared.Utils;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Reflection;

namespace MoreLevelContent.Shared.Utils
{
    internal static class TurretExtensions
    {
        internal static void GenericOperate(this Turret turret, float deltaTime, bool targetHumans, bool targetOtherCreatures, bool targetSubmarines, bool ignoreDelay, Identifier friendlyGroup)
        {
            turret.IsActive = true;

            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient)
            {
                return;
            }
            float prevTargetRotation = turret.GetPrevTargetRotation();
            float targetRotation = turret.GetTargetRotation();
            if (turret.GetUpdatePending())
            {
                float updateTimer = turret.GetUpdateTimer();
                if (updateTimer < 0.0f)
                {
#if SERVER
                    turret.Item.CreateServerEvent(turret);
#endif
                    prevTargetRotation = targetRotation;
                    updateTimer = 0.25f;
                }
                updateTimer -= deltaTime;
                turret.SetUpdateTimer(updateTimer);
            }

            float waitTimer = turret.GetWaitTimer();

            if (!ignoreDelay && waitTimer > 0)
            {
                waitTimer -= deltaTime;
                turret.SetWaitTimer(waitTimer);
                return;
            }
            Submarine closestSub = null;
            float maxDistance = 10000.0f;
            float shootDistance = turret.AIRange;
            ISpatialEntity target = null;
            float closestDist = shootDistance * shootDistance;
            if (targetHumans || targetOtherCreatures)
            {
                foreach (var character in Character.CharacterList)
                {
                    if (character == null || character.Removed || character.IsDead) { continue; }
                    if (character.Params.Group == friendlyGroup) { continue; }
                    bool isHuman = character.IsHuman || character.Params.Group == CharacterPrefab.HumanSpeciesName;
                    if (isHuman)
                    {
                        if (!targetHumans)
                        {
                            // Don't target humans if not defined to.
                            continue;
                        }
                    }
                    else if (!targetOtherCreatures)
                    {
                        // Don't target other creatures if not defined to.
                        continue;
                    }
                    float dist = Vector2.DistanceSquared(character.WorldPosition, turret.Item.WorldPosition);
                    if (dist > closestDist) { continue; }
                    target = character;
                    closestDist = dist;
                }
            }
            if (targetSubmarines)
            {
                if (target == null || target.Submarine != null)
                {
                    closestDist = maxDistance * maxDistance;
                    foreach (Submarine sub in Submarine.Loaded)
                    {
                        if (sub.Info.Type != SubmarineType.Player) { continue; }
                        float dist = Vector2.DistanceSquared(sub.WorldPosition, turret.Item.WorldPosition);
                        if (dist > closestDist) { continue; }
                        closestSub = sub;
                        closestDist = dist;
                    }
                    closestDist = shootDistance * shootDistance;
                    if (closestSub != null)
                    {
                        foreach (var hull in Hull.HullList)
                        {
                            if (!closestSub.IsEntityFoundOnThisSub(hull, true)) { continue; }
                            float dist = Vector2.DistanceSquared(hull.WorldPosition, turret.Item.WorldPosition);
                            if (dist > closestDist) { continue; }
                            target = hull;
                            closestDist = dist;
                        }
                    }
                }
            }
            if (!ignoreDelay)
            {
                if (target == null)
                {
                    // Random movement
                    waitTimer = Rand.Value(Rand.RandSync.Unsynced) < 0.98f ? 0f : Rand.Range(5f, 20f);
                    //targetRotation =
                    turret.SetTargetRotation(Rand.Range(turret.GetMinRotation(), turret.GetMaxRotation()));
                    turret.SetUpdatePending(true);
                    return;
                }
                float disorderTimer = turret.GetDisorderTimer();
                if (disorderTimer < 0)
                {
                    // Random disorder
                    disorderTimer = Rand.Range(0f, 3f);
                    waitTimer = Rand.Range(0.25f, 1f);
                    //targetRotation
                    turret.SetTargetRotation(MathUtils.WrapAngleTwoPi(targetRotation += Rand.Range(-1f, 1f)));
                    turret.SetUpdatePending(true);
                    turret.SetDisorderTimer(disorderTimer);
                    return;
                }
                else
                {
                    disorderTimer -= deltaTime;
                    turret.SetDisorderTimer(disorderTimer);
                }
            }
            if (target == null) { return; }

            float fireTargetAngle = -MathUtils.VectorToAngle(target.WorldPosition - turret.Item.WorldPosition);
            targetRotation = MathUtils.WrapAngleTwoPi(fireTargetAngle);

            // set a pending update if the rotational difference is greater than 0.1
            if (Math.Abs(targetRotation - prevTargetRotation) > 0.1f) { turret.SetUpdatePending(true); }
            Vector2 barrelDir = new Vector2((float)Math.Cos(turret.Rotation), -(float)Math.Sin(turret.Rotation));
            if (target is Hull targetHull)
            {
                // check if the current rotation intersects with the target hull
                if (!MathUtils.GetLineRectangleIntersection(turret.Item.WorldPosition, turret.Item.WorldPosition + (barrelDir * turret.AIRange), targetHull.WorldRect, out _))
                {
                    return;
                }
            }
            else
            {
                if (!turret.GenericCheckTurretAngle(fireTargetAngle)) {  return; }
                float enemyAngle = MathUtils.VectorToAngle(target.WorldPosition - turret.Item.WorldPosition);
                float turretAngle = -turret.GetRotation();
                if (Math.Abs(MathUtils.GetShortestAngle(enemyAngle, turretAngle)) > 0.15f) { return; }
            }
            
            // Make sure we're out of the wall by extending the start pos
            Vector2 start = ConvertUnits.ToSimUnits(turret.Item.WorldPosition + (barrelDir * 4));
            Vector2 end = ConvertUnits.ToSimUnits(target.WorldPosition);
            // Check that there's not other entities that shouldn't be targeted (like a friendly sub) between us and the target.
            Body worldTarget = turret.GenericCheckLOS(start, end);
            bool shoot;
            if (target.Submarine != null)
            {
                start -= target.Submarine.SimPosition;
                end -= target.Submarine.SimPosition;
                Body transformedTarget = turret.GenericCheckLOS(start, end);// CheckLineOfSight(start, end);
                shoot = turret.GenericCanShoot(transformedTarget, friendlyGroup, user: null, targetSubmarines) && (worldTarget == null || turret.GenericCanShoot(worldTarget, friendlyGroup, user: null, targetSubmarines));
            }
            else
            {
                shoot = turret.GenericCanShoot(worldTarget, null, targetSubmarines: targetSubmarines);
            }
            if (shoot)
            {
                turret.PublicTryLaunch(deltaTime, null, true);
            }
        } 

        internal static bool GenericCanShoot(this Turret turret, Body targetBody, Identifier friendlyGroup, Character user = null, bool targetSubmarines = true)
        {
            if (targetBody == null) { return false; }
            Character targetCharacter = null;
            if (targetBody.UserData is Character c)
            {
                targetCharacter = c;
            }
            else if (targetBody.UserData is Limb limb)
            {
                targetCharacter = limb.character;
            }
            if (targetCharacter != null)
            {
                if (user != null)
                {
                    if (HumanAIController.IsFriendly(user, targetCharacter))
                    {
                        return false;
                    }
                }
                if (targetCharacter.Params.Group == friendlyGroup)
                {
                    return false;
                }
            }
            else
            {
                if (targetBody.UserData is ISpatialEntity targetEntity)
                {
                    if (targetEntity is Structure s && s.Indestructible) { return false; }
                    Submarine sub = targetEntity.Submarine ?? targetEntity as Submarine;
                    if (!targetSubmarines && targetEntity is Submarine) { return false; }

                    // if there's no sub, exit
                    if (sub == null) { return false; }

                    // don't target things on the same sub as us
                    if (sub == turret.Item.Submarine) { return false; }

                    // don't target outposts, wrecks and beacons
                    if (sub.Info.IsOutpost || sub.Info.IsWreck || sub.Info.IsBeacon) { return false; }

                    // don't target subs on the same team as us
                    if (sub.TeamID == turret.Item.Submarine?.TeamID) { return false; }
                }
                else if (!(targetBody.UserData is Voronoi2.VoronoiCell cell && cell.IsDestructible))
                {
                    // Hit something else, probably a level wall
                    return false;
                }
            }
            return true;
        }

        internal static Body GenericCheckLOS(this Turret turret, Vector2 start, Vector2 end)
        {
            var collisionCategories = Physics.CollisionWall | Physics.CollisionCharacter | Physics.CollisionItem | Physics.CollisionLevel;
            Body pickedBody = Submarine.PickBody(start, end, null, collisionCategories, allowInsideFixture: true,
               customPredicate: (Fixture f) =>
               {
                   if (f.UserData is Item i && i.GetComponent<Turret>() != null) { return false; }
                   return !turret.Item.StaticFixtures.Contains(f);
               });
            return pickedBody;
        }

        // Check if the angle we need to aim at is a valid angle for this turret
        internal static bool GenericCheckTurretAngle(this Turret turret, float angle)
        {
            float midRotation = (turret.GetMinRotation() + turret.GetMaxRotation()) / 2.0f;
            while (midRotation - angle < -MathHelper.Pi) { angle -= MathHelper.TwoPi; }
            while (midRotation - angle > MathHelper.Pi) { angle += MathHelper.TwoPi; }
            return angle >= turret.GetMinRotation() && angle <= turret.GetMaxRotation();
        }

        // TODO: Rework this to use a condition weak table for values specific to the method

        // Private field access
        internal static float GetMinRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.minRotation.GetValue(turret);
        internal static float GetMaxRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.maxRotation.GetValue(turret);
        internal static float GetRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.rotation.GetValue(turret);

        internal static float GetWaitTimer(this Turret turret) => (float)TurretReflectionInfo.Instance.waitTimer.GetValue(turret);
        internal static void SetWaitTimer(this Turret turret, float val) => TurretReflectionInfo.Instance.waitTimer.SetValue(turret, val);

        internal static float GetUpdateTimer(this Turret turret) => (float)TurretReflectionInfo.Instance.updateTimer.GetValue(turret);
        internal static void SetUpdateTimer(this Turret turret, float val) => TurretReflectionInfo.Instance.updateTimer.SetValue(turret,val);

        internal static bool GetUpdatePending(this Turret turret) => (bool)TurretReflectionInfo.Instance.updatePending.GetValue(turret);
        internal static void SetUpdatePending(this Turret turret, bool val) => TurretReflectionInfo.Instance.updatePending.SetValue(turret, val);

        internal static float GetPrevTargetRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.prevTargetRotation.GetValue(turret);
        internal static void SetPrevTargetRotation(this Turret turret, float val) => TurretReflectionInfo.Instance.prevTargetRotation.SetValue(turret, val);

        internal static float GetTargetRotation(this Turret turret) => (float)TurretReflectionInfo.Instance.targetRotation.GetValue(turret);
        internal static void SetTargetRotation(this Turret turret, float val) => TurretReflectionInfo.Instance.targetRotation.SetValue(turret, val);

        internal static float GetDisorderTimer(this Turret turret) => (float)TurretReflectionInfo.Instance.disorderTimer.GetValue(turret);
        internal static void SetDisorderTimer(this Turret turret, float val) => TurretReflectionInfo.Instance.disorderTimer.SetValue(turret, val);

        // Priate method access
        internal static bool PublicTryLaunch(this Turret turret, float deltaTime, Character character = null, bool ignorePower = false) => 
            (bool)TurretReflectionInfo.Instance.tryLaunch.Invoke(turret, new object[] { deltaTime, character, ignorePower });
    }

    public class TurretReflectionInfo : Singleton<TurretReflectionInfo>
    {
        public FieldInfo minRotation;
        public FieldInfo maxRotation;
        public FieldInfo rotation;
        public FieldInfo updatePending;
        public FieldInfo waitTimer;
        public FieldInfo updateTimer;
        public FieldInfo prevTargetRotation;
        public FieldInfo targetRotation;
        public FieldInfo disorderTimer;

        public MethodInfo tryLaunch;

        public override void Setup()
        {
            // Fields
            minRotation = AccessTools.Field(typeof(Turret), "minRotation");
            maxRotation = AccessTools.Field(typeof(Turret), "maxRotation");
            rotation = AccessTools.Field(typeof(Turret), "rotation");
            updatePending = AccessTools.Field(typeof(Turret), "updatePending");
            waitTimer = AccessTools.Field(typeof(Turret), "waitTimer");
            updateTimer = AccessTools.Field(typeof(Turret), "updateTimer");
            prevTargetRotation = AccessTools.Field(typeof(Turret), "prevTargetRotation");
            targetRotation = AccessTools.Field(typeof(Turret), "targetRotation");
            disorderTimer = AccessTools.Field(typeof(Turret), "disorderTimer");

            // Methods
            tryLaunch = AccessTools.Method(typeof(Turret), "TryLaunch");

            Log.Debug("Setup turret field references");
        }
    }
}

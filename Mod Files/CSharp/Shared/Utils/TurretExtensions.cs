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
        public static Rectangle TargetHull;
        public static Vector2 Hit;
        internal static void GenericOperate(this Turret turret, float deltaTime, bool ignorePower, Identifier friendlyTag = default)
        {
            if (!ignorePower && !turret.HasPowerToShoot())
            {
                Log.Debug("No power");
                return;
            }

            turret.IsActive = true;

            if (friendlyTag.IsEmpty)
            {
                friendlyTag = turret.FriendlyTag;
            }

            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient)
            {
                Log.Debug("Client");
                return;
            }
            var updatePending = turret.GetUpdatePending();
            if (updatePending)
            {
                var updateTimer = turret.GetUpdateTimer();
                // Time for an update
                if (updateTimer < 0.0f)
                {
#if SERVER
                    turret.Item.CreateServerEvent(turret);
#endif
                    turret.SetPrevTargetRotation(turret.GetTargetRotation());
                    updateTimer = 0.25f;
                }
                turret.SetUpdateTimer(updateTimer -= deltaTime);
            }

            var waitTimer = turret.GetWaitTimer();
            if (turret.AimDelay && waitTimer > 0)
            {
                turret.SetWaitTimer(waitTimer -= deltaTime);
                Log.Debug("Waiting...");
                return;
            }

            Submarine closestSub = null;
            float maxDistance = 10000.0f;
            float shootDistance = turret.AIRange;
            ISpatialEntity target = null;
            float closestDist = shootDistance * shootDistance;
            if (turret.TargetCharacters)
            {
                foreach (var character in Character.CharacterList)
                {
                    if (!GenericIsValidTarget(character)) { continue; }
                    //float priority = turret.isSlowTurret ? character.Params.AISlowTurretPriority : character.Params.AITurretPriority;
                    float priority = character.Params.AITurretPriority;
                    if (priority <= 0) { continue; }
                    if (!GenericIsValidTargetForAutoOperate(turret, character, friendlyTag)) { continue; }
                    float dist = Vector2.DistanceSquared(character.WorldPosition, turret.Item.WorldPosition);
                    if (dist > closestDist) { continue; }
                    if (!turret.IsWithinAimingRadius(character.WorldPosition)) { continue; }
                    target = character;
                    //if (currentTarget != null && target == currentTarget)
                    //{
                    //    priority *= GetTargetPriorityModifier();
                    //}
                    closestDist = dist / priority;
                }
            }
            if (turret.TargetItems)
            {
                foreach (Item targetItem in Item.ItemList)
                {
                    if (!GenericIsValidTarget(targetItem)) { continue; }
                    float priority = targetItem.Prefab.AITurretPriority;
                    if (priority <= 0) { continue; }
                    float dist = Vector2.DistanceSquared(turret.Item.WorldPosition, targetItem.WorldPosition);
                    if (dist > closestDist) { continue; }
                    if (dist > shootDistance * shootDistance) { continue; }
                    if (!GenericIsTargetItemCloseEnough(targetItem, dist)) { continue; }
                    if (!turret.IsWithinAimingRadius(targetItem.WorldPosition)) { continue; }
                    target = targetItem;
                    // if (currentTarget != null && target == currentTarget)
                    // {
                    //     priority *= GetTargetPriorityModifier();
                    // }
                    closestDist = dist / priority;
                }
            }
            if (turret.TargetSubmarines)
            {
                if (target == null || target.Submarine != null)
                {
                    closestDist = maxDistance * maxDistance;
                    foreach (Submarine sub in Submarine.Loaded)
                    {
                        if (sub == turret.Item.Submarine) { continue; }
                        if (turret.Item.Submarine != null)
                        {
                            if (Character.IsOnFriendlyTeam(turret.Item.Submarine.TeamID, sub.TeamID)) { continue; }
                        }
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
                            // Don't check the angle, because it doesn't work on Thalamus spike. The angle check wouldn't be very important here anyway.
                            target = hull;
                            closestDist = dist;
                        }
                    }
                }
            }

            if (target == null && turret.RandomMovement)
            {
                // Random movement while there's no target
                turret.SetWaitTimer(Rand.Value(Rand.RandSync.Unsynced) < 0.98f ? 0f : Rand.Range(5f, 20f));
                turret.SetTargetRotation(Rand.Range(turret.GetMinRotation(), turret.GetMaxRotation()));
                turret.SetUpdatePending(true);
                Log.Debug("Target null, doing random movement");
                return;
            }

            if (turret.AimDelay)
            {
                if (turret.RandomAimAmount > 0)
                {
                    var ranAimTimer = turret.GetRandomAimTimer();
                    if (ranAimTimer < 0)
                    {
                        turret.SetRandomAimTimer(Rand.Range(turret.RandomAimMinTime, turret.RandomAimMaxTime));
                        turret.SetWaitTimer(Rand.Range(0.25f, 1f));
                        float randomAim = MathHelper.ToRadians(turret.RandomAimAmount);
                        float _targetRot = turret.GetTargetRotation();
                        turret.SetTargetRotation(MathUtils.WrapAngleTwoPi(_targetRot += Rand.Range(-randomAim, randomAim)));
                        turret.SetUpdatePending(true);
                    } else
                    {
                        turret.SetRandomAimTimer(ranAimTimer -= deltaTime);
                    }
                }
            }
            if (target == null)
            {
                Log.Debug("Target null");
                return;
            }
            // currentTarget = target;

            float angle = -MathUtils.VectorToAngle(target.WorldPosition - turret.Item.WorldPosition);
            var targetRot = turret.GetTargetRotation();
            targetRot = MathUtils.WrapAngleTwoPi(angle);
            if (Math.Abs(targetRot - turret.GetPrevTargetRotation()) > 0.1f) { turret.SetUpdatePending(true); }
            turret.SetTargetRotation(targetRot);

            // We ignore this currently since for some reason the intersect always fails, no idea why, it should be the same code as the live version...
            // if (target is Hull targetHull)
            // {
            //     Vector2 barrelDir = new Vector2((float)Math.Cos(turret.Rotation), -(float)Math.Sin(turret.Rotation));
            //     if (!MathUtils.GetLineRectangleIntersection(turret.Item.WorldPosition, turret.Item.WorldPosition + (barrelDir * turret.AIRange), targetHull.WorldRect, out _))
            //     {
            //         TargetHull = targetHull.WorldRect;
            //         Log.Debug("No intersection with hull");
            //         return;
            //     }
            // }
            // else
            // {
            //     if (!GenericIsWithinAimingRadius(turret, angle)) { Log.Debug("Not within aim radius");  return; }
            //     if (!GenericIsPointingTowards(turret, target.WorldPosition)) { Log.Debug("Not pointing towards"); return; }
            // }


            Vector2 start = ConvertUnits.ToSimUnits(turret.Item.WorldPosition);
            Vector2 end = ConvertUnits.ToSimUnits(target.WorldPosition);
            // Check that there's not other entities that shouldn't be targeted (like a friendly sub) between us and the target.
            Body worldTarget = turret.GenericCheckLOS(start, end);
            bool shoot;

            if (target.Submarine != null)
            {
                start -= target.Submarine.SimPosition;
                end -= target.Submarine.SimPosition;
                Body transformedTarget = turret.GenericCheckLOS(start, end);
                shoot = turret.GenericCanShoot(transformedTarget, user: null, friendlyTag, turret.TargetSubmarines) && (worldTarget == null || GenericCanShoot(turret, worldTarget, user: null, friendlyTag, turret.TargetSubmarines));
            }
            else
            {
                shoot = GenericCanShoot(turret, worldTarget, user: null, friendlyTag, turret.TargetSubmarines);
            }
            if (shoot)
            {
                turret.PublicTryLaunch(deltaTime, ignorePower: ignorePower);
                Log.Debug("We're trying to shoot!!");
            } else
            {
                Log.Debug("Can't shoot");
            }
        }

        internal static Body GenericCheckLOS(this Turret turret, Vector2 start, Vector2 end)
        {
            var collisionCategories = Physics.CollisionWall | Physics.CollisionCharacter | Physics.CollisionItem | Physics.CollisionLevel | Physics.CollisionProjectile;
            Body pickedBody = Submarine.PickBody(start, end, null, collisionCategories, allowInsideFixture: true,
               customPredicate: (Fixture f) =>
               {
                   if (f.UserData is Item i && i.GetComponent<Turret>() != null) { return false; }
                   if (f.UserData is Hull) { return false; }
                   return !turret.Item.StaticFixtures.Contains(f);
               });
            Hit = pickedBody.Position;
            return pickedBody;
        }

        internal static bool GenericCanShoot(this Turret turret, Body targetBody, Character user = null, Identifier friendlyTag = default, bool targetSubmarines = true, bool allowShootingIfNothingInWay = false)
        {
            if (targetBody == null)
            {
                //nothing in the way (not even the target we're trying to shoot) -> no point in firing at thin air
                Log.Debug("Nothing in way, not even target");
                return allowShootingIfNothingInWay;
            }
            Character targetCharacter = null;
            if (targetBody.UserData is Character c)
            {
                targetCharacter = c;
            }
            else if (targetBody.UserData is Limb limb)
            {
                targetCharacter = limb.character;
            }
            if (targetCharacter != null && !targetCharacter.Removed)
            {
                if (user != null)
                {
                    if (HumanAIController.IsFriendly(user, targetCharacter))
                    {
                        Log.Debug("Target human friendly");
                        return false;
                    }
                }
                else if (!GenericIsValidTargetForAutoOperate(turret, targetCharacter, friendlyTag))
                {
                    Log.Debug("Not valid target for auto operate");
                    // Note that Thalamus runs this even when AutoOperate is false.
                    return false;
                }
            }
            else
            {
                if (targetBody.UserData is ISpatialEntity e)
                {
                    if (e is Structure { Indestructible: true }) { Log.Debug("Target indestructable"); return false; }
                    if (!targetSubmarines && e is Submarine) { Log.Debug("Target is a sub and we don't target those"); return false; }
                    Submarine sub = e.Submarine ?? e as Submarine;
                    if (sub == null) { return true; }
                    if (sub == turret.Item.Submarine) { Log.Debug("Hit our own sub"); return false; }
                    if (sub.Info.IsOutpost || sub.Info.IsWreck || sub.Info.IsBeacon) { Log.Debug("Hit a outpost/wreck/beacon"); return false; }
                    if (sub.TeamID == turret.Item.Submarine.TeamID) { Log.Debug("Sub is on our team"); return false; }
                }
                else if (targetBody.UserData is not Voronoi2.VoronoiCell { IsDestructible: true })
                {
                    // Hit something else, probably a level wall
                    Log.Debug("Target is level wall");
                    return false;
                }
            }
            return true;
        }


        // Check if the angle we need to aim at is a valid angle for this turret
        internal static bool GenericCheckTurretAngle(this Turret turret, float angle)
        {
            float midRotation = (turret.GetMinRotation() + turret.GetMaxRotation()) / 2.0f;
            while (midRotation - angle < -MathHelper.Pi) { angle -= MathHelper.TwoPi; }
            while (midRotation - angle > MathHelper.Pi) { angle += MathHelper.TwoPi; }
            return angle >= turret.GetMinRotation() && angle <= turret.GetMaxRotation();
        }

        internal static bool GenericIsValidTarget(ISpatialEntity target)
        {
            if (target == null) { return false; }
            if (target is Character targetCharacter)
            {
                if (!targetCharacter.Enabled || targetCharacter.Removed || targetCharacter.IsDead || targetCharacter.AITurretPriority <= 0)
                {
                    return false;
                }
            }
            else if (target is Item targetItem)
            {
                if (targetItem.Removed || targetItem.Condition <= 0 || !targetItem.Prefab.IsAITurretTarget || targetItem.Prefab.AITurretPriority <= 0 || targetItem.HiddenInGame)
                {
                    return false;
                }
                if (targetItem.Submarine != null)
                {
                    return false;
                }
                if (targetItem.ParentInventory != null)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool GenericIsValidTargetForAutoOperate(Turret turret, Character target, Identifier friendlyTag)
        {
            if (!friendlyTag.IsEmpty)
            {
                if (target.SpeciesName.Equals(friendlyTag) || target.Group.Equals(friendlyTag)) { Log.Debug("In Group"); return false; }
            }
            bool isHuman = target.IsHuman || target.Group == CharacterPrefab.HumanSpeciesName;
            if (isHuman)
            {
                if (turret.Item.Submarine != null)
                {
                    if (turret.Item.Submarine.Info.IsOutpost) { Log.Debug("Outpost"); return false; }
                    // Check that the target is not in the friendly team, e.g. pirate or a hostile player sub (PvP).
                    var valid = !target.IsOnFriendlyTeam(turret.Item.Submarine.TeamID) && turret.TargetHumans;
                    Log.Debug("Target is on a friendly sub");
                    return valid;
                }
                return turret.TargetHumans;
            }
            else
            {
                Log.Debug($"Target monsters? {turret.TargetMonsters}");
                // Shouldn't check the team here, because all the enemies are in the same team (None).
                return turret.TargetMonsters;
            }
        }

        internal static bool GenericIsTargetItemCloseEnough(Item target, float sqrDist) => float.IsPositiveInfinity(target.Prefab.AITurretTargetingMaxDistance) || sqrDist < MathUtils.Pow2(target.Prefab.AITurretTargetingMaxDistance);

        internal static bool GenericIsWithinAimingRadius(Turret turret, float angle)
        {
            float min = turret.GetMinRotation(), max = turret.GetMaxRotation();
            float midRotation = (min + max) / 2.0f;
            while (midRotation - angle < -MathHelper.Pi) { angle -= MathHelper.TwoPi; }
            while (midRotation - angle > MathHelper.Pi) { angle += MathHelper.TwoPi; }
            return angle >= min && angle <= max;
        }

        internal static bool GenericIsPointingTowards(Turret turret, Vector2 targetPos)
        {
            float enemyAngle = MathUtils.VectorToAngle(targetPos - turret.Item.WorldPosition);
            float turretAngle = -turret.GetRotation();
            float maxAngleError = MathHelper.ToRadians(turret.MaxAngleOffset);
            // if (turret.MaxChargeTime > 0.0f && currentChargingState == ChargingState.WindingUp && FiringRotationSpeedModifier > 0.0f)
            // {
            //     //larger margin of error if the weapon needs to be charged (-> the bot can start charging when the turret is still rotating towards the target)
            //     maxAngleError *= 2.0f;
            // }
            return Math.Abs(MathUtils.GetShortestAngle(enemyAngle, turretAngle)) <= maxAngleError;
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

        internal static float GetRandomAimTimer(this Turret turret) => (float)TurretReflectionInfo.Instance.randomAimTimer.GetValue(turret);
        internal static void SetRandomAimTimer(this Turret turret, float val) => TurretReflectionInfo.Instance.randomAimTimer.SetValue(turret, val);


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
        public FieldInfo randomAimTimer;

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
            randomAimTimer = AccessTools.Field(typeof(Turret), "randomAimTimer");

            // Methods
            tryLaunch = AccessTools.Method(typeof(Turret), "TryLaunch");

            Log.Debug("Setup turret field references");
        }
    }
}

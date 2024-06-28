using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Voronoi2;

namespace MoreLevelContent.Shared.Utils
{
    public static class PhysUtil
    {
        // public static RayHit RayCastFirst(Vector2 point_0, Vector2 point_1)
        // {
        //     RayHit hit = new RayHit() { Hit = false };
        //     Func<Fixture, Vector2, Vector2, float, float> get_first_callback = delegate (Fixture fixture, Vector2 point, Vector2 normal, float fraction)
        //     {
        //         hit = new RayHit(fixture, point, normal);
        //         return 0;
        //     };
        // 
        //     // Summary:
        //     //     Ray-cast the world for all fixtures in the path of the ray. Your callback
        //     //     controls whether you get the closest point, any point, or n-points.  The
        //     //     ray-cast ignores shapes that contain the starting point.  Inside the callback:
        //     //     return -1: ignore this fixture and continue 
        //     //     return  0: terminate the ray cast
        //     //     return fraction: clip the ray to this point
        //     //     return 1:        don't clip the ray and continue
        //     GameMain.World.RayCast(get_first_callback, point_0, point_1);
        //     return hit;
        // }

        public static RayHit RaycastWorld(Vector2 start_simpos, Vector2 end_simpos, IEnumerable<Body> ignoredBodies = null)
        {
            RayHit hit = new RayHit() { Hit = false };


            GameMain.World.RayCast((fixture, point, normal, fraction) =>
            {
                if (!CheckFixtureCollision(fixture, ignoredBodies, Physics.CollisionLevel | Physics.CollisionWall, true, CheckForWalls)) return -1;
                hit.Hit = true;
                hit.Body = fixture.Body;
                return 0;
            }, start_simpos, end_simpos, Physics.CollisionLevel | Physics.CollisionWall);


            return hit;
        }

        public static bool WorldPositionClear(Vector2 simPos)
        {
            var aabb = new FarseerPhysics.Collision.AABB(simPos + Vector2.One, simPos - Vector2.One);
            bool clear = true;
            GameMain.World.QueryAABB((fixture) =>
            {
                if (!CheckFixtureCollision(fixture, null, Physics.CollisionLevel | Physics.CollisionWall, true, CheckForWalls)) return true;
                clear = false;
                return false;
            }, ref aabb);
            return clear;
        }

        static bool CheckForWalls(Fixture f) => (f.Body?.UserData is VoronoiCell cell && cell.Body.BodyType == BodyType.Static) ||
                    !Level.Loaded.ExtraWalls.Any(w => w.Body == f.Body);

        private static bool CheckFixtureCollision(Fixture fixture, IEnumerable<Body> ignoredBodies = null, Category? collisionCategory = null, bool ignoreSensors = true, Predicate<Fixture> customPredicate = null)
        {
            if (fixture == null ||
                (ignoreSensors && fixture.IsSensor) ||
                fixture.CollisionCategories == Category.None ||
                fixture.CollisionCategories == Physics.CollisionItem)
            {
                return false;
            }

            if (customPredicate != null && !customPredicate(fixture))
            {
                return false;
            }

            if (collisionCategory != null &&
                !fixture.CollisionCategories.HasFlag((Category)collisionCategory) &&
                !((Category)collisionCategory).HasFlag(fixture.CollisionCategories))
            {
                return false;
            }

            if (ignoredBodies != null && ignoredBodies.Contains(fixture.Body))
            {
                return false;
            }

            if (fixture.Body.UserData is Structure structure)
            {
                if (structure.IsPlatform && collisionCategory != null && !((Category)collisionCategory).HasFlag(Physics.CollisionPlatform))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public struct RayHit
    {
        public bool Hit;
        public Body Body;
        public RayHit(Body body)
        {
            Body = body;
            Hit = true;
        }
    }
}

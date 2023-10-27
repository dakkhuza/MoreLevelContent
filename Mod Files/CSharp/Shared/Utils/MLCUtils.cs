using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Voronoi2;
using static Barotrauma.Level;

namespace MoreLevelContent.Shared.Utils
{
    public static class MLCUtils
    {
        internal static string GetRandomTag(string baseTag)
        {
            int maxIndex = 1;
            while (TextManager.ContainsTag(baseTag + maxIndex))
            {
                maxIndex++;
            }
            return "mlc.lostcargo.tooslow" + Rand.Range(0, maxIndex);
        }

        internal static string GetRandomTag(string baseTag, LevelData data)
        {
            Random rand = new MTRandom(ToolBox.StringToInt(data.Seed));
            int maxIndex = 1;
            while (TextManager.ContainsTag(baseTag + maxIndex))
            {
                maxIndex++;
            }
            return "mlc.lostcargo.tooslow" + rand.Next(0, maxIndex);
        }


        internal static Vector2 PositionItemOnEdge(Item target, GraphEdge edge, float height, bool setRotation = false)
        {
            Vector2 dir = Vector2.Normalize(edge.GetNormal(edge.Cell1 ?? edge.Cell2));
            float angle = Angle(dir) - 90;
            Vector2 pos = ConvertUnits.ToSimUnits(edge.Center + (edge.GetNormal(edge.Cell1 ?? edge.Cell2) * height));
            SetItemPosition(target, pos, setRotation ? MathHelper.ToRadians(angle) : 0);
            target.Rotation = -angle;
            return dir;
        }
        internal static void SetItemPosition(Item target, Vector2 simPos, float rot) => target.SetTransform(simPos - (target.Submarine?.SimPosition ?? Vector2.Zero), rot, false);
        internal static float Angle(Vector2 dir) => (float)(MathUtils.VectorToAngle(dir) * 180 / Math.PI);

        internal static Random GetLevelRandom()
        {
            if (Level.Loaded == null)
            {
                Log.Error("Level was null when we tried to get a random instance!");
                return null;
            }

            return new MTRandom(ToolBox.StringToInt(Level.Loaded.LevelData.Seed));
        }
    }

    // We copy the whole method over here just so we can add a check
    // to see if we're too close to a beacon station because
    // trying to patch local methods through IL code is fuckin ass
    public static class SubPlacementUtils 
    {
        internal static Submarine SpawnSubOnPath(string subName, ContentFile contentFile, SubmarineType type)
        {
            var tempSW = new Stopwatch();
            FieldInfo _levelCells = AccessTools.Field(typeof(Level), "cells");
            List<VoronoiCell> cells = (List<VoronoiCell>)_levelCells.GetValue(Loaded);

            // Min distance between a sub and the start/end/other sub.
            const float minDistance = Sonar.DefaultSonarRange;
            var waypoints = WayPoint.WayPointList.Where(wp =>
                wp.Submarine == null &&
                wp.SpawnType == SpawnType.Path &&
                wp.WorldPosition.X < Loaded.EndExitPosition.X &&
                !Loaded.IsCloseToStart(wp.WorldPosition, minDistance) &&
                !Loaded.IsCloseToEnd(wp.WorldPosition, minDistance)).ToList();

            var subDoc = SubmarineInfo.OpenFile(contentFile.Path.Value);
            Rectangle subBorders = Submarine.GetBorders(subDoc.Root);
            SubmarineInfo info = new SubmarineInfo(contentFile.Path.Value)
            {
                Type = type
            };

            //place downwards by default
            var placement = info.BeaconStationInfo?.Placement ?? PlacementType.Bottom;

            // Add some margin so that the sub doesn't block the path entirely. It's still possible that some larger subs can't pass by.
            Point paddedDimensions = new Point(subBorders.Width + 3000, subBorders.Height + 3000);

            var positions = new List<Vector2>();
            var rects = new List<Rectangle>();
            int maxAttempts = 50;
            int attemptsLeft = maxAttempts;
            bool success = false;
            Vector2 spawnPoint = Vector2.Zero;
            var allCells = Loaded.GetAllCells();
            while (attemptsLeft > 0)
            {
                if (attemptsLeft < maxAttempts)
                {
                    Debug.WriteLine($"Failed to position the sub {subName}. Trying again.");
                }
                attemptsLeft--;
                if (TryGetSpawnPoint(out spawnPoint))
                {
                    success = TryPositionSub(subBorders, subName, placement, ref spawnPoint);
                    if (success)
                    {
                        break;
                    }
                    else
                    {
                        positions.Clear();
                    }
                }
                else
                {
                    DebugConsole.NewMessage($"Failed to find any spawn point for the sub: {subName} (No valid waypoints left).", Color.Red);
                    break;
                }
            }
            tempSW.Stop();
            if (success)
            {
                Debug.WriteLine($"Sub {subName} successfully positioned to {spawnPoint} in {tempSW.ElapsedMilliseconds} (ms)");
                tempSW.Restart();
                Submarine sub = new Submarine(info);
                // sub.ShowSonarMarker = false;
                // sub.DockedTo.ForEach(s => s.ShowSonarMarker = false);
                // sub.PhysicsBody.FarseerBody.BodyType = BodyType.Static;
                // sub.TeamID = CharacterTeamType.None;

                tempSW.Stop();
                Debug.WriteLine($"Sub {sub.Info.Name} loaded in {tempSW.ElapsedMilliseconds} (ms)");
                sub.SetPosition(spawnPoint);
                // wreckPositions.Add(sub, positions);
                // blockedRects.Add(sub, rects);
                return sub;
            }
            else
            {
                DebugConsole.NewMessage($"Failed to position wreck {subName}. Used {tempSW.ElapsedMilliseconds} (ms).", Color.Red);
                return null;
            }

            bool TryPositionSub(Rectangle subBorders, string subName, PlacementType placement, ref Vector2 spawnPoint)
            {
                positions.Add(spawnPoint);
                bool bottomFound = TryRaycast(subBorders, placement, ref spawnPoint);
                positions.Add(spawnPoint);

                bool leftSideBlocked = IsSideBlocked(subBorders, false);
                bool rightSideBlocked = IsSideBlocked(subBorders, true);
                int step = 5;
                if (rightSideBlocked && !leftSideBlocked)
                {
                    bottomFound = TryMove(subBorders, placement, ref spawnPoint, -step);
                }
                else if (leftSideBlocked && !rightSideBlocked)
                {
                    bottomFound = TryMove(subBorders, placement, ref spawnPoint, step);
                }
                else if (!bottomFound)
                {
                    if (!leftSideBlocked)
                    {
                        bottomFound = TryMove(subBorders, placement, ref spawnPoint, -step);
                    }
                    else if (!rightSideBlocked)
                    {
                        bottomFound = TryMove(subBorders, placement, ref spawnPoint, step);
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid position {spawnPoint}. Does not touch the ground.");
                        return false;
                    }
                }
                positions.Add(spawnPoint);
                bool isBlocked = IsBlocked(spawnPoint, subBorders.Size - new Point(step + 50));
                if (isBlocked)
                {
                    rects.Add(ToolBox.GetWorldBounds(spawnPoint.ToPoint(), subBorders.Size));
                    Debug.WriteLine($"Invalid position {spawnPoint}. Blocked by level walls.");
                }
                else if (!bottomFound)
                {
                    Debug.WriteLine($"Invalid position {spawnPoint}. Does not touch the ground.");
                }
                else
                {
                    var sp = spawnPoint;
                    if (Loaded.Wrecks.Any(w => Vector2.DistanceSquared(w.WorldPosition, sp) < minDistance * minDistance))
                    {
                        Debug.WriteLine($"Invalid position {spawnPoint}. Too close to other wreck(s).");
                        return false;
                    }
                    if (Loaded.BeaconStation != null)
                    {
                        if (Vector2.DistanceSquared(Loaded.BeaconStation.WorldPosition, sp) < minDistance * minDistance)
                        {
                            return false;
                        }
                    }
                }
                return !isBlocked && bottomFound;

                bool TryMove(Rectangle subBorders, PlacementType placement, ref Vector2 spawnPoint, float amount)
                {
                    float maxMovement = 5000;
                    float totalAmount = 0;
                    bool foundBottom = TryRaycast(subBorders, placement, ref spawnPoint);
                    while (!IsSideBlocked(subBorders, amount > 0))
                    {
                        foundBottom = TryRaycast(subBorders, placement, ref spawnPoint);
                        totalAmount += amount;
                        spawnPoint = new Vector2(spawnPoint.X + amount, spawnPoint.Y);
                        if (Math.Abs(totalAmount) > maxMovement)
                        {
                            Debug.WriteLine($"Moving the sub {subName} failed.");
                            break;
                        }
                    }
                    return foundBottom;
                }
            }

            bool TryGetSpawnPoint(out Vector2 spawnPoint)
            {
                spawnPoint = Vector2.Zero;
                while (waypoints.Any())
                {
                    var wp = waypoints.GetRandom(Rand.RandSync.ServerAndClient);
                    waypoints.Remove(wp);
                    if (!IsBlocked(wp.WorldPosition, paddedDimensions))
                    {
                        spawnPoint = wp.WorldPosition;
                        return true;
                    }
                }
                return false;
            }

            bool TryRaycast(Rectangle subBorders, PlacementType placement, ref Vector2 spawnPoint)
            {
                // Shoot five rays and pick the highest hit point.
                int rayCount = 5;
                var positions = new Vector2[rayCount];
                bool hit = false;
                for (int i = 0; i < rayCount; i++)
                {
                    float quarterWidth = subBorders.Width * 0.25f;
                    Vector2 rayStart = spawnPoint;
                    switch (i)
                    {
                        case 1:
                            rayStart = new Vector2(spawnPoint.X - quarterWidth, spawnPoint.Y);
                            break;
                        case 2:
                            rayStart = new Vector2(spawnPoint.X + quarterWidth, spawnPoint.Y);
                            break;
                        case 3:
                            rayStart = new Vector2(spawnPoint.X - quarterWidth / 2, spawnPoint.Y);
                            break;
                        case 4:
                            rayStart = new Vector2(spawnPoint.X + quarterWidth / 2, spawnPoint.Y);
                            break;
                    }
                    var simPos = ConvertUnits.ToSimUnits(rayStart);
                    var body = Submarine.PickBody(simPos, new Vector2(simPos.X, placement == PlacementType.Bottom ? -1 : Loaded.Size.Y + 1),
                        customPredicate: f => f.Body == Loaded.TopBarrier || f.Body == Loaded.BottomBarrier || (f.Body?.UserData is VoronoiCell cell && cell.Body.BodyType == BodyType.Static && !Loaded.ExtraWalls.Any(w => w.Body == f.Body)),
                        collisionCategory: Physics.CollisionLevel | Physics.CollisionWall);
                    if (body != null)
                    {
                        positions[i] =
                            ConvertUnits.ToDisplayUnits(Submarine.LastPickedPosition) +
                            new Vector2(0, subBorders.Height / 2 * (placement == PlacementType.Bottom ? 1 : -1));
                        hit = true;
                    }
                }
                float highestPoint = placement == PlacementType.Bottom ? positions.Max(p => p.Y) : positions.Min(p => p.Y);
                spawnPoint = new Vector2(spawnPoint.X, highestPoint);
                return hit;
            }

            bool IsSideBlocked(Rectangle subBorders, bool front)
            {
                // Shoot three rays and check whether any of them hits.
                int rayCount = 3;
                Vector2 halfSize = subBorders.Size.ToVector2() / 2;
                Vector2 quarterSize = halfSize / 2;
                var positions = new Vector2[rayCount];
                for (int i = 0; i < rayCount; i++)
                {
                    float dir = front ? 1 : -1;
                    Vector2 rayStart;
                    Vector2 to;
                    switch (i)
                    {
                        case 1:
                            rayStart = new Vector2(spawnPoint.X + halfSize.X * dir, spawnPoint.Y + quarterSize.Y);
                            to = new Vector2(spawnPoint.X + (halfSize.X - quarterSize.X) * dir, rayStart.Y);
                            break;
                        case 2:
                            rayStart = new Vector2(spawnPoint.X + halfSize.X * dir, spawnPoint.Y - quarterSize.Y);
                            to = new Vector2(spawnPoint.X + (halfSize.X - quarterSize.X) * dir, rayStart.Y);
                            break;
                        case 0:
                        default:
                            rayStart = spawnPoint;
                            to = new Vector2(spawnPoint.X + halfSize.X * dir, rayStart.Y);
                            break;
                    }
                    Vector2 simPos = ConvertUnits.ToSimUnits(rayStart);
                    if (Submarine.PickBody(simPos, ConvertUnits.ToSimUnits(to),
                        customPredicate: f => f.Body?.UserData is VoronoiCell cell,
                        collisionCategory: Physics.CollisionLevel | Physics.CollisionWall) != null)
                    {
                        return true;
                    }
                }
                return false;
            }

            bool IsBlocked(Vector2 pos, Point size, float maxDistanceMultiplier = 1)
            {
                float maxDistance = size.Multiply(maxDistanceMultiplier).ToVector2().LengthSquared();
                Rectangle bounds = ToolBox.GetWorldBounds(pos.ToPoint(), size);
                if (Loaded.Ruins.Any(r => ToolBox.GetWorldBounds(r.Area.Center, r.Area.Size).IntersectsWorld(bounds)))
                {
                    return true;
                }
                if (Loaded.Caves.Any(c =>
                        ToolBox.GetWorldBounds(c.Area.Center, c.Area.Size).IntersectsWorld(bounds) ||
                        ToolBox.GetWorldBounds(c.StartPos, new Point(1500)).IntersectsWorld(bounds)))
                {
                    return true;
                }
                return cells.Any(c => c.Body != null && Vector2.DistanceSquared(pos, c.Center) <= maxDistance && c.BodyVertices.Any(v => bounds.ContainsWorld(v)));
            }
        }
    }
}

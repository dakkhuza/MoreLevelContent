using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
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
    public class TrackingSonarMarker
    {
        public (LocalizedString Label, Vector2 Position) CurrentPosition { get; private set; }
        private readonly LocalizedString label;

        public TrackingSonarMarker(float updateInterval, Func<Vector2> getPositionFunc, LocalizedString sonarLabel)
        {
            _updateInterval = updateInterval;
            _getPositionFunc = getPositionFunc;
            label = sonarLabel;
            Init();
        }

        internal TrackingSonarMarker(float updateInterval, Submarine submarine, LocalizedString sonarLabel)
        {
            _updateInterval = updateInterval;
            _getPositionFunc = () => submarine.WorldPosition;
            label = sonarLabel;
            Init();
        }

        readonly float _updateInterval;
        readonly Func<Vector2> _getPositionFunc;
        private float _timeSinceLastUpdate;

        private void Init() => CurrentPosition = (label, _getPositionFunc.Invoke());

        public void Update(float delta)
        {
            _timeSinceLastUpdate += delta;
            if (_timeSinceLastUpdate > _updateInterval)
            {
                _timeSinceLastUpdate = 0;
                CurrentPosition = (label, _getPositionFunc.Invoke());
            }
        }
    }

    public class ReputationDamageTracker : IDisposable
    {
        const float MaxDamagePerSecond = 5.0f;
        const float MaxDamagePerFrame = MaxDamagePerSecond * (float)Timing.Step;

        private readonly Submarine _sub;
        private readonly float _threshold;
        private readonly float _maxRepLoss;
        private readonly float _decayPerSec;
        private readonly float _decayDelay;
        private float _accumulatedDamage;
        private bool _displayedWarning = false;
        private float _lostRep = 0.0f;
        private float _decayTimer;
        
        internal ReputationDamageTracker(Submarine subToTrack, float threshold = 20f, float maxRepLoss = 20f, float decay = 1f, float decayDelay = 5f)
        {
            Hooks.Instance.OnStructureDamaged += OnStructureDamaged;
            _sub = subToTrack;
            _threshold = threshold;
            _maxRepLoss = maxRepLoss;
            _decayPerSec = decay * (float)Timing.Step;
            _decayDelay = decayDelay;
        }

        public void Update()
        {
            if (_accumulatedDamage > 0 && _decayTimer <= 0)
            {
                _accumulatedDamage -= _decayPerSec;
            }

            if (_decayTimer > 0)
            {
                _decayTimer -= (float)Timing.Step;
            }
        }

        private void OnStructureDamaged(Structure structure, float damageAmount, Character character)
        {
            if (character == null || !character.IsPlayer) { return; }
            if (structure?.Submarine == null || structure.Submarine != _sub) { return; }
            // ignore interior walls so gun fights don't cause rep loss
            if (structure.Prefab.Tags.Contains("inner")) { return; }

            _accumulatedDamage += MathHelper.Clamp(damageAmount, 0, MaxDamagePerFrame);
            _decayTimer = _decayDelay;

            if (_accumulatedDamage < _threshold) return;

            if (!_displayedWarning)
            {
#if SERVER
                GameMain.Server?.SendChatMessage(TextManager.GetServerMessage("distress.ghostship.damagenotification")?.Value, ChatMessageType.Default);
#endif
#if CLIENT
                if (GameMain.IsSingleplayer)
                {
                    GameMain.GameSession?.CrewManager?.AddSinglePlayerChatMessage(
                        TextManager.Get("mlc.info1")?.Value, TextManager.Get("distress.ghostship.damagenotification")?.Value,
                        ChatMessageType.MessageBox, null);
                }
#endif
                _accumulatedDamage = 0;
                _displayedWarning = true;
                Log.Debug("Showed warning message");
                return;
            }

            if (GameMain.GameSession?.Campaign?.Map?.CurrentLocation?.Reputation != null)
            {
                if (_lostRep >= _maxRepLoss) return;

                var reputationLoss = damageAmount * Reputation.ReputationLossPerWallDamage;
                reputationLoss = Math.Min(reputationLoss, 10); // clamp rep loss to a value 0-10
                GameMain.GameSession.Campaign.Map.CurrentLocation.Reputation.AddReputation(-reputationLoss);
                _lostRep += reputationLoss;
            }
        }

        public void Dispose()
        {
            // Don't know if I really need to do this but why not
            Hooks.Instance.OnStructureDamaged -= OnStructureDamaged;
            GC.SuppressFinalize(this);
        }
    }

    public static class MissionUtils
    {
        internal static bool TryGetInterestingPosition(PositionType positionType, float minDistFromSubs, out Point position)
        {
            if (!Loaded.PositionsOfInterest.Any())
            {
                position = new Point(Loaded.Size.X / 2, Loaded.Size.Y / 2);
                Log.Debug("Failed to find point, default to middle of level");
                return false;
            }

            List<InterestingPosition> suitablePositions = Loaded.PositionsOfInterest.FindAll(p => positionType.HasFlag(p.PositionType));

            if (positionType.HasFlag(PositionType.MainPath) || positionType.HasFlag(PositionType.SidePath))
            {
                suitablePositions.RemoveAll(p => Loaded.IsPositionInsideWall(p.Position.ToVector2()));
            }

            if (!suitablePositions.Any())
            {
                Log.Debug("Failed to find point, no positions not inside walls");
                position = Loaded.PositionsOfInterest[Rand.Int(Loaded.PositionsOfInterest.Count, Rand.RandSync.ServerAndClient)].Position;
                return false;
            }

            List<InterestingPosition> farEnoughPositions = new List<InterestingPosition>(suitablePositions);
            if (minDistFromSubs > 0.0f)
            {
                int beforeFilter = farEnoughPositions.Count;
                int filtered = farEnoughPositions.RemoveAll(p => Vector2.DistanceSquared(p.Position.ToVector2(), Loaded.StartPosition) < minDistFromSubs * minDistFromSubs);
                Log.Debug($"Removed {filtered} positions, after filer {farEnoughPositions.Count}, before: {beforeFilter}");
            }

            if (!farEnoughPositions.Any())
            {
                string errorMsg = "Could not find a position of interest far enough from the submarines. (PositionType: " + positionType + ", minDistFromSubs: " + minDistFromSubs + ")\n" + Environment.StackTrace.CleanupStackTrace();
                Log.Error(errorMsg);

                float maxDist = 0.0f;
                position = suitablePositions.First().Position;
                foreach (InterestingPosition pos in suitablePositions)
                {
                    float dist = Submarine.Loaded.Sum(s =>
                        Submarine.MainSubs.Contains(s) ? Vector2.DistanceSquared(s.WorldPosition, pos.Position.ToVector2()) : 0.0f);
                    if (dist > maxDist)
                    {
                        position = pos.Position;
                        maxDist = dist;
                    }
                }

                return false;
            }

            position = farEnoughPositions[Rand.Int(farEnoughPositions.Count, Rand.RandSync.ServerAndClient)].Position;
            Log.Debug("Found position!");
            return true;
        }

        public static Vector2 GetPosInRect(Rectangle rect, Random rand)
        {
            float x = rand.Next(0, rect.Width);
            float y = rand.Next(0, rect.Height);
            y = Math.Clamp(y, rect.Height / 3, rect.Height / 2);
            return new Vector2(rect.X + x, rect.Y + y);
        }
        static FieldInfo _waypointTagsField;

        internal static void TagSubmarineWaypoints(this Submarine submarine, string tag)
        {
            if (_waypointTagsField == null)
            {
                _waypointTagsField = typeof(WayPoint).GetField("tags", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            var validWaypoints = WayPoint.WayPointList.Where(wp => wp.Submarine == submarine && wp.SpawnType == SpawnType.Human);
            foreach (var waypoint in validWaypoints)
            {
                HashSet<Identifier> tags = (HashSet<Identifier>)_waypointTagsField.GetValue(waypoint);
                _ = tags.Add(tag);
            }
        }
    }

    // We copy the whole method over here just so we can add a check
    // to see if we're too close to a beacon station because
    // trying to patch local methods through IL code is fuckin ass
    public static class SubPlacementUtils
    {
        internal static Submarine SpawnSubOnPath(string subName, ContentFile contentFile, SubmarineType type, PlacementType placementType = PlacementType.Bottom) => SpawnSubOnPath(subName, contentFile.Path.Value, type, placementType);

        internal static Submarine SpawnSubOnPath(string subName, string path, SubmarineType type, PlacementType placementType = PlacementType.Bottom)
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

            var subDoc = SubmarineInfo.OpenFile(path);
            Rectangle subBorders = Submarine.GetBorders(subDoc.Root);
            SubmarineInfo info = new SubmarineInfo(path)
            {
                Type = type
            };

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
                    success = TryPositionSub(subBorders, subName, placementType, ref spawnPoint);
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
        internal static void PositionSubmarine(Submarine submarine, PositionType positionType)
        {
            float dist = Sonar.DefaultSonarRange * 2;
            if (MissionUtils.TryGetInterestingPosition(positionType, dist, out Point point))
            {
                Vector2 spawnPos = point.ToVector2();
                Point subSize = submarine.GetDockedBorders().Size;
                int graceDistance = 500; // the sub still spawns awkwardly close to walls, so this helps. could also be given as a parameter instead
                spawnPos = submarine.FindSpawnPos(spawnPos, new Point(subSize.X + graceDistance, subSize.Y + graceDistance));
                submarine.SetPosition(spawnPos);
            }
        }

        internal static void SetCrushDepth(Submarine sub, bool inf = false)
        {
            if (inf)
            {
                sub.SetCrushDepth(float.MaxValue);
                return;
            }
            float depth = Math.Max(sub.RealWorldCrushDepth, Submarine.MainSub.RealWorldCrushDepth);
            depth = Math.Max(depth, Loaded.GetRealWorldDepth(sub.Position.Y) + 1000);
            sub.SetCrushDepth(depth);
        }
    }

}

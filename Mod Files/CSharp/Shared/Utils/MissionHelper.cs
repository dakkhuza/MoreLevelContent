using Barotrauma;
using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    public static class MissionUtils
    {
        internal static void PositionSubmarine(Submarine submarine, PositionType positionType)
        {
            float dist = Loaded.Size.X * 0.5f;
            if (TryGetInterestingPosition(positionType, dist, out Point point))
            {
                Vector2 spawnPos = point.ToVector2();
                Point subSize = submarine.GetDockedBorders().Size;
                int graceDistance = 500; // the sub still spawns awkwardly close to walls, so this helps. could also be given as a parameter instead
                spawnPos = submarine.FindSpawnPos(spawnPos, new Point(subSize.X + graceDistance, subSize.Y + graceDistance));
                submarine.SetPosition(spawnPos);
            }
        }


        private static bool TryGetInterestingPosition(PositionType positionType, float minDistFromSubs, out Point position)
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
}

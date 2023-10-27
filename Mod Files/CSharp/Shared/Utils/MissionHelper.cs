using Barotrauma;
using Barotrauma.Networking;
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
            if (character == null || !character.IsPlayer) return;
            if (structure?.Submarine == null || structure.Submarine != _sub) { return; }

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

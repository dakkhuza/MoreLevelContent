using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System;
using MoreLevelContent.Shared.Utils;
using static Barotrauma.Level;
using Voronoi2;
using MoreLevelContent.Shared.Generation;
using HarmonyLib;

namespace MoreLevelContent.Shared.AI
{
    public class CaveAiConfig
    {
        public Identifier Entity => "thalamus";
        public Identifier DefensiveAgent => "Leucocyte";
        public string OffensiveAgent => "Terminalcell";
        public string Brain => "thalamusbrain_cave";
        public string Spawner => "cellspawnorgan_cave";
        public float AgentSpawnDelay => 10;
        public float AgentSpawnDelayRandomFactor => 0.25f;
        public float AgentSpawnDelayDifficultyMultiplier => 1.0f;
        public float AgentSpawnCountDifficultyMultiplier => 1.0f;
        public int MaxAgentCount => 30;
        public bool KillAgentsWhenEntityDies => true;
        public float DeadEntityColorMultiplier => 0.5f;
        public float DeadEntityColorFadeOutTime => 1;
    }

    partial class CaveAI : IServerSerializable
    {
        public bool IsAlive { get; private set; }

        public readonly List<Item> ThalamusItems;
        public readonly Cave Cave;
        private readonly List<Turret> turrets = new List<Turret>();
        private readonly List<Item> spawnOrgans = new List<Item>();
        private readonly List<VoronoiCell> spawnPoints = new List<VoronoiCell>();
        private readonly Item brain;
        // Auto operate turrets need to have a submarine to work
        public readonly Submarine DummySub;

        private bool initialCellsSpawned;

        public readonly CaveAiConfig Config = new CaveAiConfig();

        private bool IsClient => GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient;

        private bool IsThalamus(MapEntityPrefab entityPrefab) => IsThalamus(entityPrefab, Config.Entity);

        private static IEnumerable<T> GetThalamusEntities<T>(Submarine wreck, Identifier tag) where T : MapEntity => GetThalamusEntities(wreck, tag).Where(e => e is T).Select(e => e as T);

        private static IEnumerable<MapEntity> GetThalamusEntities(Submarine wreck, Identifier tag) => MapEntity.MapEntityList.Where(e => e.Submarine == wreck && e.Prefab != null && IsThalamus(e.Prefab, tag));

        private static bool IsThalamus(MapEntityPrefab entityPrefab, Identifier tag) => entityPrefab.HasSubCategory("thalamus") || entityPrefab.Tags.Contains(tag);

        public CaveAI(List<Item> allThalamusItems, GraphEdge spawnEdge, Cave cave)
        {
            Log.Debug($"it {allThalamusItems == null} se: {spawnEdge == null} cave: {cave == null}");
            this.Cave = cave;
            DummySub = new Submarine(new SubmarineInfo(), showErrorMessages: false)
            {
                TeamID = CharacterTeamType.None,
                ShowSonarMarker = false
            };
            DummySub.PhysicsBody.BodyType = FarseerPhysics.BodyType.Static;
            DummySub.Info.Type = SubmarineType.EnemySubmarine;

            allThalamusItems.ForEach(i => i.Submarine = DummySub);

            var thalamusPrefabs = ItemPrefab.Prefabs.Where(p => IsThalamus(p));
            var brainPrefab = thalamusPrefabs.Where(p => p.Tags.Contains(Config.Brain)).FirstOrDefault();
            if (brainPrefab == null)
            {
                DebugConsole.ThrowError($"WreckAI: Could not find any brain prefab with the tag {Config.Brain}! Cannot continue. Failed to create wreck AI.");
                return;
            }
            ThalamusItems = allThalamusItems;

            brain = new Item(brainPrefab, Vector2.Zero, null);
            ThalamusItems.Add(brain);
            _ = MLCUtils.PositionItemOnEdge(brain, spawnEdge, 120, true);

            // Setup spawner organs
            spawnPoints = cave.Tunnels.SelectMany(t => t.Cells.Where(c => c.CellType != CellType.Solid && c.CellType != CellType.Removed)).ToList();

            foreach (var item in allThalamusItems)
            {
                var turret = item.GetComponent<Turret>();
                if (turret != null)
                {
                    turrets.Add(turret);
                    turret.AutoOperate = false;
                }
                if (item.HasTag(Config.Spawner))
                {
                    if (!spawnOrgans.Contains(item))
                    {
                        spawnOrgans.Add(item);
                    }
                }
            }

            // need to setup positions for initial cells to spawn
            IsAlive = true;
        }

        private readonly List<Item> destroyedOrgans = new List<Item>();
        public void Update(float deltaTime)
        {
            // General AI management
            if (!IsAlive) { return; }
            if (Cave == null)
            {
                Remove();
                return;
            }
            if (brain == null || brain.Removed || brain.Condition <= 0)
            {
                Kill();
                return;
            }

            // Manage organs
            destroyedOrgans.Clear();
            foreach (var organ in spawnOrgans)
            {
                if (organ.Condition <= 0)
                {
                    destroyedOrgans.Add(organ);
                }
            }
            destroyedOrgans.ForEach(o => spawnOrgans.Remove(o));

            // Manage agro
            bool someoneNearby = false;
            float minDist = Sonar.DefaultSonarRange * 2.0f;
            foreach (Submarine submarine in Submarine.Loaded)
            {
                if (submarine.Info.Type != SubmarineType.Player) { continue; }
                if (Vector2.DistanceSquared(submarine.WorldPosition, Cave.StartPos.ToVector2()) < minDist * minDist)
                {
                    someoneNearby = true;
                    break;
                }
            }
            foreach (Character c in Character.CharacterList)
            {
                if (c != Character.Controlled && !c.IsRemotePlayer) { continue; }
                if (Vector2.DistanceSquared(c.WorldPosition, Cave.StartPos.ToVector2()) < minDist * minDist)
                {
                    someoneNearby = true;
                    break;
                }
            }
            if (!someoneNearby) { return; }
            OperateTurrets(deltaTime);
            if (!IsClient)
            {
                if (!initialCellsSpawned)
                {
                    SpawnInitialCells();
                    ClearCave();
                }
                UpdateReinforcements(deltaTime);
            }
        }

        private void ClearCave()
        {
            var wallsNearCave = Loaded.ExtraWalls.Where(w => 
            w.Cells.Any(c => c.IsDestructible && 
            (Cave.Area.Contains(c.Center) || 
            Vector2.DistanceSquared(Cave.StartPos.ToVector2(), c.Center) < Sonar.DefaultSonarRange * Sonar.DefaultSonarRange)));

            foreach (var wall in wallsNearCave)
            {
                if (wall is DestructibleLevelWall destructible)
                {
                    destructible.Destroy();
                    destructible.NetworkUpdatePending = true;
                }
            }
        }

        private void SpawnInitialCells()
        {
            int closeBrainCells = Rand.Range(5, 8);

            for (int i = 0; i < closeBrainCells; i++)
            {
                if (!TrySpawnCell(out _, brain)) { break; }
            }

            int initalCells = Rand.Range(5, MaxCellCount);
            for (int i = 0; i < initalCells; i++)
            {
                if (!TrySpawnCell(out _)) { break; }
            }

            initialCellsSpawned = true;
        }

        public void Kill()
        {
            ThalamusItems.ForEach(i => i.Condition = 0);
            foreach (var turret in turrets)
            {
                // Snap all tendons
                foreach (Item item in turret.ActiveProjectiles)
                {
                    if (item.GetComponent<Projectile>()?.IsStuckToTarget ?? false)
                    {
                        item.Condition = 0;
                    }
                }
            }
            FadeOutColors();
            protectiveCells.ForEach(c => c.OnDeath -= OnCellDeath);
            if (!IsClient)
            {
                if (Config != null)
                {
                    if (Config.KillAgentsWhenEntityDies)
                    {
                        protectiveCells.ForEach(c => c.Kill(CauseOfDeathType.Unknown, null));
                        if (!string.IsNullOrWhiteSpace(Config.OffensiveAgent))
                        {
                            foreach (var character in Character.CharacterList)
                            {
                                // Kills ALL offensive agents that are near the thalamus. Not the ideal solution, 
                                // but as long as spawning is handled via status effects, I don't know if there is any better way.
                                // In practice there shouldn't be terminal cells from different thalamus organisms at the same time.
                                // And if there was, the distance check should prevent killing the agents of a different organism.
                                if (character.SpeciesName == Config.OffensiveAgent)
                                {
                                    // Sonar distance is used also for wreck positioning. No wreck should be closer to each other than this.
                                    float maxDistance = Sonar.DefaultSonarRange;
                                    if (Vector2.DistanceSquared(character.WorldPosition, Cave.StartPos.ToVector2()) < maxDistance * maxDistance)
                                    {
                                        character.Kill(CauseOfDeathType.Unknown, null);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            protectiveCells.Clear();
            IsAlive = false;
        }

        partial void FadeOutColors();

        public void Remove()
        {
            Kill();
            ThalamusItems?.Clear();
            Log.Debug("Removed thalacave");
        }

        public void RemoveThalamusItems()
        {
            foreach (MapEntity thalamusItem in ThalamusItems)
            {
                if (thalamusItem.Removed) continue;
                thalamusItem.Remove();
            }
        }

        // The client doesn't use these, so we don't have to sync them.
        private readonly List<Character> protectiveCells = new List<Character>();
        private float cellSpawnTimer;

        private int MaxCellCount => CalculateCellCount(5, Config.MaxAgentCount);

        private int CalculateCellCount(int minValue, int maxValue)
        {
            if (maxValue == 0) { return 0; }
            float difficulty = Level.Loaded?.Difficulty ?? 0.0f;
            float t = MathUtils.InverseLerp(0, 100, difficulty * Config.AgentSpawnCountDifficultyMultiplier);
            return (int)Math.Round(MathHelper.Lerp(minValue, maxValue, t));
        }

        private float GetSpawnTime()
        {
            float randomFactor = Config.AgentSpawnDelayRandomFactor;
            float delay = Config.AgentSpawnDelay;
            float min = delay;
            float max = delay * 6;
            float difficulty = Level.Loaded?.Difficulty ?? 0.0f;
            float t = difficulty * Config.AgentSpawnDelayDifficultyMultiplier * Rand.Range(1 - randomFactor, 1 + randomFactor);
            return MathHelper.Lerp(max, min, MathUtils.InverseLerp(0, 100, t));
        }

        void UpdateReinforcements(float deltaTime)
        {
            if (spawnOrgans.Count == 0) { return; }
            cellSpawnTimer -= deltaTime;
            if (cellSpawnTimer < 0)
            {
                TrySpawnCell(out _, spawnOrgans.GetRandomUnsynced());
                cellSpawnTimer = GetSpawnTime();
            }
        }

        bool TrySpawnCell(out Character cell, ISpatialEntity targetEntity = null)
        {
            cell = null;
            if (protectiveCells.Count >= MaxCellCount) { return false; }
            Vector2 worldSpawnPosition = targetEntity == null ? spawnPoints.GetRandomUnsynced().Center : targetEntity.WorldPosition;

            // Don't add items in the list, because we want to be able to ignore the restrictions for spawner organs.
            cell = Character.Create(Config.DefensiveAgent, worldSpawnPosition, ToolBox.RandomSeed(8), hasAi: true, createNetworkEvent: true);
            protectiveCells.Add(cell);
            cell.OnDeath += OnCellDeath;
            cellSpawnTimer = GetSpawnTime();
            return true;
        }

        void OperateTurrets(float deltaTime)
        {
            foreach (var turret in turrets)
            {
                turret.UpdateAutoOperate(deltaTime, true, Config.Entity);
            }
        }

        void OnCellDeath(Character character, CauseOfDeath causeOfDeath) => protectiveCells.Remove(character);

#if SERVER
        public void ServerEventWrite(IWriteMessage msg, Client client, NetEntityEvent.IData extraData = null)
        {
            msg.WriteBoolean(IsAlive);
        }
#endif
    }
}

﻿using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics.Dynamics;
using FarseerPhysics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Voronoi2;
using static Barotrauma.Level;
using MoreLevelContent.Shared.Utils;
using MoreLevelContent.Shared.AI;

namespace MoreLevelContent.Shared.Generation
{
    public class CaveGenerationDirector : GenerationDirector<CaveGenerationDirector>
    {
        public override bool Active => true;

        private static MethodInfo level_findawayfrompoint;
        private static MethodInfo level_generatecave;
        private static MethodInfo level_calcdistfields;
        private static FieldInfo cave_genparams;
        private static FieldInfo item_statusEffectList;
        private static MethodInfo item_rotation;
        private static PropertyInfo statusEffect_offset;
        private static PropertyInfo statusEffect_characterSpawn_offset;

        private CaveAI ActiveThalaCave;

        public override void Setup()
        {
            MethodInfo Level_Generate = AccessTools.Method(typeof(Level), "Generate", new Type[] { typeof(bool), typeof(Location), typeof(Location) });
            _ = Main.Harmony.Patch(Level_Generate, transpiler: new HarmonyMethod(AccessTools.Method(typeof(CaveGenerationDirector), nameof(CaveGenerationDirector.SwapCavesTranspiler))));

            MethodInfo level_update = AccessTools.Method(typeof(Level), "Update");
            _ = Main.Harmony.Patch(level_update, postfix: new HarmonyMethod(AccessTools.Method(typeof(CaveGenerationDirector), nameof(CaveGenerationDirector.Update))));

            MethodInfo level_remove = AccessTools.Method(typeof(Level), "Remove");
            _ = Main.Harmony.Patch(level_remove, postfix: new HarmonyMethod(AccessTools.Method(typeof(CaveGenerationDirector), nameof(CaveGenerationDirector.Remove))));

            MethodInfo submarine_CullEntities = AccessTools.Method(typeof(Submarine), "CullEntities");
            _ = Main.Harmony.Patch(submarine_CullEntities, postfix: new HarmonyMethod(AccessTools.Method(typeof(CaveGenerationDirector), nameof(CaveGenerationDirector.OnCull))));

            level_generatecave = AccessTools.Method(typeof(Level), "GenerateCave");
            level_findawayfrompoint = AccessTools.Method(typeof(Level), "FindPosAwayFromMainPath"); 
            level_calcdistfields = AccessTools.Method(typeof(Level), "CalculateTunnelDistanceField");
            cave_genparams = AccessTools.Field(typeof(Cave), "CaveGenerationParams");
            item_rotation = AccessTools.PropertySetter(typeof(Item), "RotationRad");
            item_statusEffectList = AccessTools.Field(typeof(Item), "statusEffectLists");
            statusEffect_offset = AccessTools.Property(typeof(StatusEffect), "Offset");
            statusEffect_characterSpawn_offset = AccessTools.Property(typeof(StatusEffect.CharacterSpawnInfo), "Offset");
        }

        const float MIN_DIST = Sonar.DefaultSonarRange * 2;

        static void OnCull(List<MapEntity> ___visibleEntities)
        {
            if (Instance.ActiveThalaCave != null)
            {
                ___visibleEntities.AddRange(Instance.ActiveThalaCave.ThalamusItems);
            }
        }

        static void Update(float deltaTime)
        {
            // Don't run the ai in editors or if we're the client
            if (GameMain.GameScreen.IsEditor || Main.IsClient) return;
            Instance.ActiveThalaCave?.Update(deltaTime);
        }

        static void Remove()
        {
            Instance.ActiveThalaCave?.Remove();
            Instance.ActiveThalaCave = null;
        }

        static IEnumerable<CodeInstruction> SwapCavesTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var code = new List<CodeInstruction>(instructions);
            bool finished = false;
            Log.Debug("transpiling...");
            for (int i = 0; i < code.Count; i++) // -1 since we will be checking i + 1
            {
                yield return code[i];

                if (!finished && code[i].opcode == OpCodes.Ldc_I4_S && (sbyte)code[i].operand == 14)
                {
                    if (code[i + 1].Calls(AccessTools.Method(typeof(Level), "GenerateEqualityCheckValue")))
                    {
                        Log.Debug($"Found insertion point at {i}!");
                        // endfinally
                        i++;
                        yield return code[i]; // ldc.i4.0
                        i++;
                        yield return code[i]; // stloc.s
                        i++;
                        yield return code[i]; //br
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CaveGenerationDirector), nameof(CaveGenerationDirector.TrySpawnThalaCave))); // index of cell around curIndex
                    }
                }
            }
        }

        static void TrySpawnThalaCave()
        {
            if (Loaded.GenerationParams.ThalamusProbability == 0 || Instance.ActiveThalaCave != null) return;
            var caveParams = CaveGenerationParams.CaveParams.Where(c =>
            {
                Log.Debug(c.Identifier.ToString());
                return c.Identifier == "thalamuscave";
            }).FirstOrDefault();

            if (caveParams == null)
            {
                Log.Error("Unable to find thalacave perfab!");
                return;
            }


            const int REQUIRED_EDGE_COUNT = 4;

            foreach (var cave in Loaded.Caves)
            {
                // find valid caves
                bool isValid = cave.Tunnels.Where(
                    t => {
                    int count = t.Cells.Where(
                        c =>
                        {
                            bool result = CanSeeMainPath(c, out List<GraphEdge> edges);
                            return result;
                        }
                    ).Count();
                    Log.Debug($"Valid Edges: {count} Require Edges: {REQUIRED_EDGE_COUNT}");
                    return count >= REQUIRED_EDGE_COUNT; 
                }).Any();

                if (isValid)
                {
                    Log.Debug("Valid cave found!");
                    cave_genparams.SetValue(cave, caveParams);
                    MakeThalaCave(cave);
                    return;
                }
            }
            Log.Debug("No valid caves found");
        }

        static bool CanSeeMainPath(VoronoiCell cell, out List<GraphEdge> validEdges)
        {
            validEdges = new List<GraphEdge>();

            foreach (var edge in cell.Edges.Where(e => e.NextToMainPath || e.NextToSidePath))
            {
                return true;
            }
            return false;
        }

        private static bool IsThalamus(MapEntityPrefab entityPrefab) => entityPrefab.HasSubCategory("thalamus");
        private static Vector2 ClosestPathPoint(Cave cave)
        {
            var pathPoints = Loaded.PositionsOfInterest.Where(poi => poi.PositionType == PositionType.MainPath || poi.PositionType == PositionType.SidePath).ToList();

            Vector2 closestPos = Vector2.Zero;
            float dist = float.PositiveInfinity;
            foreach (var point in pathPoints)
            {
                float newDist = Vector2.DistanceSquared(point.Position.ToVector2(), cave.StartPos.ToVector2());
                if (newDist < dist)
                {
                    closestPos = point.Position.ToVector2();
                    dist = newDist;
                }
            }
            return closestPos;
        }

        static void MakeThalaCave(Cave cave)
        {
            // a1ek3WII <- seed

            List<VoronoiCell> caveWallCells = GetCaveWallCells(cave);

            // Spawn thalamus items
            List<Item> thalamusItems = new List<Item>();
            var thalamusPrefabs = ItemPrefab.Prefabs.Where(p => IsThalamus(p));
            var gunPrefab = thalamusPrefabs.Where(p => p.Tags.Contains("fleshgun_cave") && p.Tags.Contains("turret")).FirstOrDefault();
            var largeSpikePrefab = thalamusPrefabs.Where(p => p.Tags.Contains("fleshspike_cave")).FirstOrDefault();
            var smallSpikePrefab = thalamusPrefabs.Where(p => p.Tags.Contains("fleshspike_cave_small")).FirstOrDefault();
            var spawnerPrefab = thalamusPrefabs.Where(p => p.Tags.Contains("cellspawnorgan_cave")).FirstOrDefault();
            var ammosackPrefab = thalamusPrefabs.Where(p => p.Tags.Contains("fleshgunequipment_cave")).FirstOrDefault();
            var storageOrgan = thalamusPrefabs.Where(p => p.Tags.Contains("storageorgan_cave")).FirstOrDefault();

            var pathPoint = ClosestPathPoint(cave);
            List<GraphEdge> entranceEdges = GetEdgesFacingPoint();

            var insideEdges = caveWallCells.SelectMany(c =>
                c.Edges.Where(e =>
                e.IsSolid &&
                !CanEdgeSeePathPoint(e) &&
                WideEnough(e) &&
                !InsideExtraWall(e)
                )).ToList();

            GraphEdge brainEdge = null;
            float closestDist = float.PositiveInfinity;
            float curDist;
            foreach (var edge in insideEdges)
            {
                curDist = Vector2.DistanceSquared(edge.Center, cave.EndPos.ToVector2());
                if (curDist < closestDist)
                {
                    brainEdge = edge;
                    closestDist = curDist;
                }
            }
            // Prevent other organs from spawning inside the brain
            _ = insideEdges.Remove(brainEdge);
            List<Item> fleshGuns = new List<Item>();

            CreateOffensiveItems();
            CreateDefensiveItems();

            Instance.ActiveThalaCave = new CaveAI(thalamusItems, brainEdge, cave);

            _ = Loaded.PositionsOfInterest.RemoveAll(poi => poi.Cave == cave);



            // Methods

            void CreateOffensiveItems()
            {
                Queue<Action> offensiveItems = new Queue<Action>();
                // Limit offensive items to a max of 8
                for (int i = 0; i < Math.Min(entranceEdges.Count, 8); i++)
                {
                    if (i % 2 == 0) offensiveItems.Enqueue(SpawnFleshSpike);
                    else offensiveItems.Enqueue(SpawnFleshGun);
                }
                while (offensiveItems.Count > 0)
                {
                    offensiveItems.Dequeue().Invoke();
                }
            }

            const float MIN_DIST_BETWEEN_ORGANS = 800;
            void CreateDefensiveItems()
            {
                int totalSpawnLocations = insideEdges.Count;
                int cellSpawns = totalSpawnLocations / 4;
                for (int i = 0; i < cellSpawns; i++)
                {
                    SpawnCellSpawner(GetEdge(insideEdges, true));
                }

                foreach (var fleshgun in fleshGuns)
                {
                    var ammosack = SpawnOrgan(ammosackPrefab, GetEdge(insideEdges, true));
                    fleshgun.AddLinked(ammosack);
                }
                // Ensure there is always 4 organs
                int organCount = Math.Max(insideEdges.Count / 8, 4);

                // Don't let the organ count go over the remaining valid edges
                organCount = Math.Min(insideEdges.Count, organCount);

                for (int i = 0; i < organCount; i++)
                {
                    if (insideEdges.Count == 0) break;
                    _ = SpawnOrgan(storageOrgan, GetEdge(insideEdges, true));
                }
            }

            void SpawnFleshGun()
            {
                Item fleshgun = new Item(gunPrefab, Vector2.Zero, null);
                thalamusItems.Add(fleshgun);
                fleshGuns.Add(fleshgun);
                GraphEdge edge = GetEdge(entranceEdges);
                if (edge == null) return;
                int radius = fleshgun.StaticBodyConfig.GetAttributeInt("radius", 0);
                Vector2 dir = MLCUtils.PositionItemOnEdge(fleshgun, edge, radius + 50);
                float angle = Angle(dir) - 90;
                Turret turret = fleshgun.GetComponent<Turret>();
                turret.RotationLimits = new Vector2(-angle - 90, -angle + 90);
                turret.AimDelay = false;

                Log.Debug($"Placed fleshgun at {fleshgun.Position}");
            }

            void SpawnFleshSpike()
            {
                Item spike = new Item(largeSpikePrefab, Vector2.Zero, null);
                
                thalamusItems.Add(spike);

                GraphEdge edge = GetEdge(entranceEdges);
                if (edge == null) return;
                int height = spike.StaticBodyConfig.GetAttributeInt("height", 0);
                Vector2 dir = MLCUtils.PositionItemOnEdge(spike, edge, height + 15);
                float angle = Angle(dir);
                spike.SpriteDepth = 1;
                Turret turret = spike.GetComponent<Turret>();
                turret.RotationLimits = new Vector2(-angle, -angle);

                // config status effects
                Dictionary<ActionType, List<StatusEffect>> dic = (Dictionary<ActionType, List<StatusEffect>>)item_statusEffectList.GetValue(spike);
                if (dic?.TryGetValue(ActionType.OnUse, out List<StatusEffect> effects) ?? false)
                {
                    // Adjust offsets of on use status effects to match our angle
                    foreach (var effect in effects)
                    {
                        float dist = effect.Offset.Y;
                        float turretRot = -angle;
                        float turretRotRad = MathHelper.ToRadians(turretRot);
                        Vector2 newOffset = new Vector2((float)Math.Cos(turretRotRad), (float)Math.Sin(turretRotRad)) * dist;
                        statusEffect_offset.SetValue(effect, newOffset);
                        foreach (var spawnEffect in effect.SpawnCharacters)
                        {
                            dist = spawnEffect.Offset.Y;
                            newOffset = new Vector2((float)Math.Cos(turretRotRad), (float)Math.Sin(turretRotRad)) * dist;
                            statusEffect_characterSpawn_offset.SetValue(spawnEffect, newOffset);
                        }
                    }
                }

                Log.Debug($"Placed spike at {spike.Position}");

            }

            void SpawnCellSpawner(GraphEdge edge)
            {
                Item spawner = new Item(spawnerPrefab, Vector2.Zero, null);
                thalamusItems.Add(spawner);
                Vector2 dir = MLCUtils.PositionItemOnEdge(spawner, edge, 80, true);
            }

            Item SpawnOrgan(ItemPrefab organPrefab, GraphEdge edge)
            {
                Item organ = new Item(organPrefab, Vector2.Zero, null);
                thalamusItems.Add(organ);
                Vector2 dir = MLCUtils.PositionItemOnEdge(organ, edge, 60, true);
                return organ;
            }






            GraphEdge GetEdge(List<GraphEdge> edges, bool removeClose = false)
            {
                if (!edges.Any()) return null;
                GraphEdge edge = edges.GetRandom(Rand.RandSync.ServerAndClient);
                _ = edges.Remove(edge);

                // Remove all valid edges that are too close to this edge
                if (removeClose) _ = edges.RemoveAll(e => Vector2.DistanceSquared(edge.Center, e.Center) < MIN_DIST_BETWEEN_ORGANS * MIN_DIST_BETWEEN_ORGANS);
                return edge;
            }

            float Angle(Vector2 dir) => (float)(MathUtils.VectorToAngle(dir) * 180 / Math.PI);

            List<GraphEdge> GetEdgesFacingPoint()
            {
                List<GraphEdge> edges = new List<GraphEdge>();

                caveWallCells
                .ForEach(c =>
                {
                    edges.AddRange(c.Edges.Where(e => 
                    e.IsSolid && 
                    WideEnough(e) &&
                    FacingPathPoint(e) &&
                    CanEdgeSeePathPoint(e)
                    ).ToList());
                });
                return edges;
            }

            bool FacingPathPoint(GraphEdge e) => Vector2.Dot(Vector2.Normalize(e.GetNormal(null)), Vector2.Normalize(e.Center - pathPoint)) >= 0;
            bool WideEnough(GraphEdge e, float size = 200) => Vector2.DistanceSquared(e.Point1, e.Point2) > size * size;
            bool CanEdgeSeePathPoint(GraphEdge e) => !PhysUtil.RaycastWorld(e.SimPosition(), ConvertUnits.ToSimUnits(pathPoint), new List<Body> {  }).Hit;
            bool CanPosSeePathPoint(Vector2 simPos) => !PhysUtil.RaycastWorld(simPos, ConvertUnits.ToSimUnits(pathPoint), new List<Body> { }).Hit;
            bool InsideExtraWall(GraphEdge e)
            {
                // this doesn't work at all
                // SAD
                bool cell1 = false;
                bool cell2 = false;
                if (e.Cell1 != null)
                {
                    cell1 = Loaded.ExtraWalls.Any(w => w.IsPointInside(e.Cell1.Center));
                }
                if (e.Cell2 != null)
                {
                    cell2 = Loaded.ExtraWalls.Any(w => w.IsPointInside(e.Cell2.Center));
                }
                return cell1 || cell2;
            }

            Vector2 GetEdgeDir(GraphEdge edge) => edge.GetNormal(null);
        }

        static List<VoronoiCell> GetCaveWallCells(Cave cave)
        {
            List<VoronoiCell> caveWalls = new List<VoronoiCell>();

            foreach (var caveCell in cave.Tunnels.SelectMany(t => t.Cells))
            {
                foreach (var edge in caveCell.Edges)
                {
                    if (!edge.NextToCave) { continue; }
                    if (edge.Cell1?.CellType == CellType.Solid && !caveWalls.Contains(edge.Cell1))
                    {
                        caveWalls.Add(edge.Cell1);
                    }
                    if (edge.Cell2?.CellType == CellType.Solid && !caveWalls.Contains(edge.Cell2))
                    {
                        caveWalls.Add(edge.Cell2);
                    }
                }
            }
            return caveWalls;
        }
    }

    public static class GraphEdgeExtensions
    {
        public static Vector2 SimPosition(this GraphEdge edge) => ConvertUnits.ToSimUnits(edge.Center);
    }
}
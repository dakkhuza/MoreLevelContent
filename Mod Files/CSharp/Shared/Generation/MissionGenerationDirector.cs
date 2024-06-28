
using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation.Interfaces;
using MoreLevelContent.Shared.Store;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Voronoi2;
using static Barotrauma.Level;

namespace MoreLevelContent.Shared.Generation
{
    public class MissionGenerationDirector : GenerationDirector<MissionGenerationDirector>, IGenerateSubmarine, IGenerateNPCs
    {
        public override bool Active => true;

        readonly Queue<SubmarineSpawnRequest> SubCreationQueue = new();
        readonly Queue<DecoSpawnRequest> DecoCreationQueue = new();
        readonly Queue<Submarine> AutoFillQueue = new();
        internal delegate void OnSubmarineCreated(Submarine createdSubmarine);
        internal delegate void OnDecoCreated(List<Submarine> decoItems, Cave decoratedCave);
        public static List<(Vector2, Vector2)> DebugPoints = new();

        internal static void RequestSubmarine(SubmarineSpawnRequest info) =>
            Instance.SubCreationQueue.Enqueue(info);
        internal static void RequestStaticSubmarine(ContentFile contentFile, OnSubmarineCreated onSubmarineCreated, bool autoFill = true) => 
            Instance.RequestStaticSub(contentFile, onSubmarineCreated, autoFill);
        internal static void RequestSubmarine(ContentFile contentFile, OnSubmarineCreated onSubmarineCreated, bool autoFill = true) => 
            Instance.RequestSub(contentFile, onSubmarineCreated, autoFill);
        internal static void RequestDecorate(List<ContentFile> files, OnDecoCreated onDecoCreated, bool autoFill = false) => Instance.RequestDeco(files, onDecoCreated, autoFill);

        struct DecoSpawnRequest
        {
            public List<ContentFile> ContentFiles;
            public OnDecoCreated Callback;
            public bool AutoFill;

            public DecoSpawnRequest(List<ContentFile> contentFiles, OnDecoCreated callback, bool autoFill)
            {
                ContentFiles = contentFiles;
                Callback = callback;
                AutoFill = autoFill;
            }
        }

        internal struct SubmarineSpawnRequest
        {
            public ContentFile File;
            public OnSubmarineCreated Callback;
            public bool AutoFill = false;
            public bool AllowStealing = true;
            public AutoFillPrefix Prefix = AutoFillPrefix.None;
            public SubSpawnPosition SpawnPosition = SubSpawnPosition.PathWall;
            public PlacementType PlacementType = PlacementType.Bottom;
            public bool IgnoreCrushDpeth = true;

            public SubmarineSpawnRequest()
            {
                File = null;
                Callback = null;
            }

            public enum AutoFillPrefix
            {
                None,
                Wreck,
                Abandoned
            }
        }

        void RequestStaticSub(ContentFile contentFile, OnSubmarineCreated onSubmarineCreated, bool autoFill)
        {
            SubCreationQueue.Enqueue(new SubmarineSpawnRequest() 
            { 
                File = contentFile, 
                Callback = onSubmarineCreated,
                AutoFill = autoFill
            });
            Log.Debug("Enqueued spawn request for submarine");
        }

        void RequestSub(ContentFile contentFile, OnSubmarineCreated onSubmarineCreated, bool autoFill)
        {
            SubCreationQueue.Enqueue(new SubmarineSpawnRequest() { 
                File = contentFile,
                Callback = onSubmarineCreated,
                AutoFill = autoFill,
                SpawnPosition = SubSpawnPosition.PathWall
        });
            Log.Debug("Enqueued spawn request for submarine on path");
        }

        void RequestDeco(List<ContentFile> files, OnDecoCreated onDecoCreated, bool autoFill)
        {
            DecoCreationQueue.Enqueue(new DecoSpawnRequest(files, onDecoCreated, autoFill));
            Log.Debug($"Enqueued spawn request for cave decoration with {files.Count} items");
        }

        public void GenerateSub()
        {
            SpawnConstructionSite();
            SpawnRelayStation();
            SpawnRequestedSubs();
            DecorateCaves();
        }

        void SpawnRequestedSubs()
        {
            DebugPoints.Clear();
            while (SubCreationQueue.Count > 0)
            {
                SubmarineSpawnRequest request = SubCreationQueue.Dequeue();
                string subName = System.IO.Path.GetFileNameWithoutExtension(request.File.Path.Value);
                Submarine submarine;
                if (request.SpawnPosition == SubSpawnPosition.PathWall)
                {
                    submarine = SpawnSubOnPath(subName, request.File, ignoreCrushDepth: request.IgnoreCrushDpeth, placementType: request.PlacementType);
                } else
                {
                    if (request.SpawnPosition == SubSpawnPosition.AbyssIsland)
                    {
                        submarine = PositionAbyssCave(request);
                    } else
                    {
                        submarine = SpawnSub(request.File);
                    }
                }

                if (submarine != null)
                {
                    Log.Debug($"Spawned requested submarine {subName}");
                    request.Callback.Invoke(submarine);

                    if (request.AutoFill)
                    {
                        foreach (Item item in Item.ItemList)
                        {
                            if (item.Submarine != submarine) { continue; }
                            if (item.NonInteractable) { continue; }
                            item.AllowStealing = request.AllowStealing;
                            if (item.GetRootInventoryOwner() is Character) { continue; }
                            int len = item.Tags.Length;
                            for (int i = 0; i < len; i++)
                            {
                                if (request.Prefix != SubmarineSpawnRequest.AutoFillPrefix.None)
                                {
                                    item.AddTag($"{request.Prefix}{item.Tags[i]}");
                                }
                            }

                            foreach (var container in item.GetComponents<ItemContainer>())
                            {
                                container.AutoFill = true;
                            }
                        }
                        Log.Debug("Filed auto fill request for submarine");
                        AutoFillQueue.Enqueue(submarine);
                    }
                }
                else
                {
                    Log.Error($"Failed to spawn requested submarine!");
                }
            }
        }

        void SpawnConstructionSite()
        {
            if (Level.Loaded.LevelData.MLC().HasBeaconConstruction)
            {
                Submarine beacon = SpawnSubOnPath("Construction Site", BeaconConstStore.Instance.GetBeaconForLevel(), ignoreCrushDepth: true, SubmarineType.EnemySubmarine);
                beacon.PhysicsBody.BodyType = BodyType.Static;
                Level.Loaded.MLC().BeaconConstructionStation = beacon;
                Item storageItem = Item.ItemList.Find(it => it.Submarine == beacon && it.GetComponent<ItemContainer>() != null && it.Tags.Contains("dropoff"));
                if (storageItem == null)
                {
                    Log.Error($"Unable to find the drop off point for beacon construction {beacon.Info.Name}!");
                    return;
                }
                Level.Loaded.MLC().DropOffPoint = storageItem;
                
            }
        }

        void SpawnRelayStation()
        {
            if (!Loaded?.LevelData?.MLC()?.HasRelayStation ?? true) return;
            Log.Debug("Trying to spawn relay station");
            Submarine relayStation = SpawnSubOnPath("Relay Station", CablePuzzleMission.SubmarineFile, ignoreCrushDepth: true, SubmarineType.EnemySubmarine, PlacementType.Top);
            if (relayStation == null)
            {
                Log.Error("Failed to spawn relay station");
                return;
            }
            Log.Debug("Spawned relay station");
            relayStation.PhysicsBody.FarseerBody.BodyType = FarseerPhysics.BodyType.Static;
            relayStation.TeamID = CharacterTeamType.FriendlyNPC;
            relayStation.ShowSonarMarker = false;
            Loaded.MLC().RelayStation = relayStation;
        }

        void DecorateCaves()
        {
            Log.Debug("Decorating Caves");
            while (DecoCreationQueue.Count > 0)
            {
                var request = DecoCreationQueue.Dequeue();
                Cave cave = Loaded.Caves.GetRandom(Rand.RandSync.ServerAndClient);
                List<Submarine> deco = new();

                foreach (var file in request.ContentFiles)
                {
                    try
                    {
                        Log.Debug($"Trying to spawn deco {file.Path}");
                        var tunnel = cave.Tunnels.GetRandom(Rand.RandSync.ServerAndClient);
                        var pos = tunnel.WayPoints.GetRandom(Rand.RandSync.ServerAndClient);
                        Submarine sub = SpawnSubAtPosition("Cave Deco", file, pos.Position);
                        if (sub != null) deco.Add(sub);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.ToString());
                    }
                }

                request.Callback?.Invoke(deco, cave);
            }
        }

        public override void Setup() => BeaconConstStore.Instance.Setup();
        public void SpawnNPCs()
        {
            Log.Debug("Auto filling subs");
            if (AutoFillQueue.Count == 0) Log.Debug("No subs in autofill queue");
            while (AutoFillQueue.Count > 0)
            {
                try
                {
                    Submarine submarine = AutoFillQueue.Dequeue();
                    AutofillSub(submarine);
                    Log.Debug("Auto filled submarine");
                } catch(Exception e)
                {
                    Log.Debug(e.ToString());
                }
            }
        }

        void PositionAbyss(Submarine sub)
        {
            Log.Error("Position Type Abyss is not implemented");
        }
        
        Submarine PositionAbyssCave(SubmarineSpawnRequest request)
        {
            var subDoc = SubmarineInfo.OpenFile(request.File.Path.Value);
            Rectangle subBorders = Submarine.GetBorders(subDoc.Root);
            SubmarineInfo info = new SubmarineInfo(request.File.Path.Value);

            int maxAttempts = 25;
            int attemptsLeft = maxAttempts;
            var rand = MLCUtils.GetLevelRandom();
            var validIslands = Loaded.AbyssIslands.Where(i => !Loaded.Caves.Any(c => c.Area.Intersects(i.Area))).ToList();
            if (!validIslands.Any())
            {
                // If we found NO islands, tolerate spawning on caves
                validIslands = Loaded.AbyssIslands;
            }
            Vector2 startPoint = default;
            bool foundPos = false;
            int offset = 1;
            int dir = request.PlacementType == PlacementType.Bottom ? 1 : -1;


            SpawnOnIsland(validIslands);

            if (!foundPos)
            {
                Log.Error("Failed to find a spawn position");
                return null;
            }

            Submarine sub = new Submarine(info);
            sub.SetPosition(startPoint, forceUndockFromStaticSubmarines: false);
            return sub;

            void SpawnOnIsland(List<AbyssIsland> islands)
            {
                var island = validIslands.GetRandom(rand);
                if (island == null)
                {
                    Log.Debug("Failed to find a valid island to spawn on");
                    return;
                }
                startPoint = island.Area.Center.ToVector2();

                // Check if position is overlapping
                while (attemptsLeft > 0)
                {
                    if (TryPosition())
                    {
                        foundPos = true;
                        return;
                    }
                    offset++;
                }

                // We found no position for this island
                // Remove it and try again if we still have islands left
                _ = validIslands.Remove(island);
                attemptsLeft = maxAttempts;
                if (islands.Count == 0)
                {
                    Log.Error("NO valid abyss islands found :((");
                    return;
                }
                SpawnOnIsland(islands);

                bool TryPosition()
                {
                    float halfHeight = subBorders.Height / 10;
                    float startY = startPoint.Y + (halfHeight * offset * dir);
                    float x1 = startPoint.X - (subBorders.Width / 2);
                    float x2 = startPoint.X;
                    float x3 = startPoint.X + (subBorders.Width / 2);


                    Vector2 rayStart = new Vector2(x2, startY);
                    Vector2 to = new Vector2(x2, startY - (halfHeight * dir));
                    DebugPoints.Add((rayStart, to));

                    Vector2 simPos = ConvertUnits.ToSimUnits(rayStart);
                    if (Submarine.PickBody(simPos, ConvertUnits.ToSimUnits(to),
                        customPredicate: f => f.Body?.UserData is VoronoiCell cell,
                        collisionCategory: Physics.CollisionLevel | Physics.CollisionWall,
                        allowInsideFixture: true) != null)
                    {
                        return false;
                    }
                    else
                    {
                        startPoint = new Vector2(to.X, to.Y + ((subBorders.Height / 2) * dir));
                        //for (int i = 0; i < 50; i++)
                        //{
                        //    if (Slam()) break;
                        //}
                        return true;
                    }

                    bool Slam()
                    {
                        if (Submarine.PickBody(simPos, ConvertUnits.ToSimUnits(to),
                            customPredicate: f => f.Body?.UserData is VoronoiCell cell,
                            collisionCategory: Physics.CollisionLevel | Physics.CollisionWall,
                            allowInsideFixture: true) != null)
                        {
                            return false;
                        } else
                        {
                            return true;
                        }
                    }
                }

            }
        }

        private Submarine SpawnSub(ContentFile contentFile)
        {
            SubmarineInfo info = new SubmarineInfo(contentFile.Path.Value);
            Submarine sub = new Submarine(info);
            return sub;
        }

        private Submarine SpawnSubAtPosition(string subName, ContentFile contentFile, Vector2 spawnPoint)
        {
            var tempSW = new Stopwatch();

            // Min distance between a sub and the start/end/other sub.
            var subDoc = SubmarineInfo.OpenFile(contentFile.Path.Value);
            Rectangle subBorders = Submarine.GetBorders(subDoc.Root);
            Point paddedDimensions = new Point(subBorders.Width, subBorders.Height);

            var positions = new List<Vector2>();
            var rects = new List<Rectangle>();
            int maxAttempts = 50;
            int attemptsLeft = maxAttempts;
            bool success = true;
            var allCells = Loaded.GetAllCells();
            while (attemptsLeft > 0)
            {
                if (attemptsLeft < maxAttempts)
                {
                    Log.Debug($"Failed to position the sub {subName}. Trying again.");
                }
                attemptsLeft--;
                success = TryPositionSub(subBorders, subName, ref spawnPoint);
                if (success)
                {
                    break;
                }
                else
                {
                    positions.Clear();
                }
            }

            tempSW.Stop();
            if (success)
            {
                Log.Debug($"Sub {subName} successfully positioned to {spawnPoint} in {tempSW.ElapsedMilliseconds} (ms)");
                tempSW.Restart();
                try
                {
                    SubmarineInfo info = new SubmarineInfo(contentFile.Path.Value);
                    Submarine sub = new Submarine(info);
                    tempSW.Stop();
                    Log.Debug($"Sub {sub.Info.Name} loaded in {tempSW.ElapsedMilliseconds} (ms)");
                    sub.PhysicsBody.BodyType = BodyType.Static;
                    sub.SetPosition(spawnPoint);
                    return sub;
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                    return null;
                }
            }
            else
            {
                Log.Error($"Failed to position wreck {subName}. Used {tempSW.ElapsedMilliseconds} (ms).");
                return null;
            }

            bool TryPositionSub(Rectangle subBorders, string subName, ref Vector2 spawnPoint)
            {
                positions.Add(spawnPoint);
                bool bottomFound = TryRaycastToBottom(subBorders, ref spawnPoint);
                positions.Add(spawnPoint);

                bool leftSideBlocked = IsSideBlocked(subBorders, false);
                bool rightSideBlocked = IsSideBlocked(subBorders, true);
                int step = 5;
                if (rightSideBlocked && !leftSideBlocked)
                {
                    bottomFound = TryMove(subBorders, ref spawnPoint, -step);
                }
                else if (leftSideBlocked && !rightSideBlocked)
                {
                    bottomFound = TryMove(subBorders, ref spawnPoint, step);
                }
                else if (!bottomFound)
                {
                    if (!leftSideBlocked)
                    {
                        bottomFound = TryMove(subBorders, ref spawnPoint, -step);
                    }
                    else if (!rightSideBlocked)
                    {
                        bottomFound = TryMove(subBorders, ref spawnPoint, step);
                    }
                    else
                    {
                        Log.Debug($"Invalid position {spawnPoint}. Does not touch the ground.");
                        return false;
                    }
                }
                positions.Add(spawnPoint);
                bool isBlocked = IsBlocked(spawnPoint, subBorders.Size - new Point(step + 50));
                if (isBlocked)
                {
                    rects.Add(ToolBox.GetWorldBounds(spawnPoint.ToPoint(), subBorders.Size));
                    Log.Debug($"Invalid position {spawnPoint}. Blocked by level walls.");
                }
                else if (!bottomFound)
                {
                    Log.Debug($"Invalid position {spawnPoint}. Does not touch the ground.");
                }
                else
                {
                    var sp = spawnPoint;
                }
                return !isBlocked && bottomFound;

                bool TryMove(Rectangle subBorders, ref Vector2 spawnPoint, float amount)
                {
                    float maxMovement = 5000;
                    float totalAmount = 0;
                    bool foundBottom = TryRaycastToBottom(subBorders, ref spawnPoint);
                    while (!IsSideBlocked(subBorders, amount > 0))
                    {
                        foundBottom = TryRaycastToBottom(subBorders, ref spawnPoint);
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

            bool TryRaycastToBottom(Rectangle subBorders, ref Vector2 spawnPoint)
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
                    var body = Submarine.PickBody(simPos, new Vector2(simPos.X, -1),
                        customPredicate: f => f.Body?.UserData is VoronoiCell cell && cell.Body.BodyType == BodyType.Static && !Loaded.ExtraWalls.Any(w => w.Body == f.Body),
                        collisionCategory: Physics.CollisionLevel | Physics.CollisionWall);
                    if (body != null)
                    {
                        positions[i] = ConvertUnits.ToDisplayUnits(Submarine.LastPickedPosition) + new Vector2(0, subBorders.Height / 2);
                        hit = true;
                    }
                }
                float highestPoint = positions.Max(p => p.Y);
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
                return Loaded.GetAllCells().Any(c => c.Body != null && Vector2.DistanceSquared(pos, c.Center) <= maxDistance && c.BodyVertices.Any(v => bounds.ContainsWorld(v)));
            }
        }


        public enum SubSpawnPosition
        {
            Path,
            PathWall,
            Abyss,
            AbyssIsland
        }
    }
}

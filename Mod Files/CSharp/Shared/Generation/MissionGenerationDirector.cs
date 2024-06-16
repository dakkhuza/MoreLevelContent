
using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
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

        struct SubmarineSpawnRequest
        {
            public ContentFile ContentFile;
            public OnSubmarineCreated Callback;
            public bool AutoFill;
            public PositionType SpawnPosition;

            public SubmarineSpawnRequest(ContentFile submarineFile, OnSubmarineCreated callback, bool autoFill)
            {
                ContentFile = submarineFile;
                Callback = callback;
                AutoFill = autoFill;
                SpawnPosition = PositionType.Wreck;
            }
        }

        void RequestStaticSub(ContentFile contentFile, OnSubmarineCreated onSubmarineCreated, bool autoFill)
        {
            SubCreationQueue.Enqueue(new SubmarineSpawnRequest(contentFile, onSubmarineCreated, autoFill));
            Log.Debug("Enqueued spawn request for submarine");
        }

        void RequestSub(ContentFile contentFile, OnSubmarineCreated onSubmarineCreated, bool autoFill)
        {
            SubCreationQueue.Enqueue(new SubmarineSpawnRequest(contentFile, onSubmarineCreated, autoFill) { SpawnPosition = PositionType.MainPath });
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
            SpawnRequestedSubs();
            DecorateCaves();
        }

        void SpawnRequestedSubs()
        {
            while (SubCreationQueue.Count > 0)
            {
                var request = SubCreationQueue.Dequeue();
                string subName = System.IO.Path.GetFileNameWithoutExtension(request.ContentFile.Path.Value);

                Submarine submarine = request.SpawnPosition == PositionType.Wreck
                    ? SpawnSubOnPath(subName, request.ContentFile)
                    : SpawnSub(request.ContentFile);

                if (submarine != null)
                {
                    Log.Debug($"Spawned requested submarine {subName}");
                    request.Callback.Invoke(submarine);

                    if (request.AutoFill)
                    {
                        foreach (Item item in Item.ItemList)
                        {
                            if (item.Submarine != submarine) { continue; }
                            if (item.GetRootInventoryOwner() is Character) { continue; }
                            if (item.NonInteractable) { continue; }
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
                Submarine beacon = SpawnSubOnPath("Beacon Station", BeaconConstStore.Instance.GetBeaconForLevel(), ignoreCrushDepth: true, SubmarineType.EnemySubmarine);
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

    }
}

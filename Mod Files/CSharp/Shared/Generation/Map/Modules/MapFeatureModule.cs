using Barotrauma;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using MoreLevelContent.Shared.Utils;
using static Barotrauma.Level;
using MoreLevelContent.Shared.Data;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class MapFeatureModule : MapModule
    {
        private static List<MapFeature> _Features = new();
        private static Dictionary<Identifier, MapFeature> _IdentifierToFeature = new();
        private List<Location> _DisallowedLocations;

        protected override void InitProjSpecific()
        {
            // Build table of map features
            _Features.Clear();
            _DisallowedLocations = new();
            var features = MissionPrefab.Prefabs.Where(m => m.Tags.Contains("mapfeatureset"));
            var featureDict = new Dictionary<Identifier, MapFeature>();
            foreach (var item in features)
            {
                var config = item.ConfigElement;
                foreach (var elm in config.GetChildElements("MapFeature"))
                {
                    var feature = new MapFeature(elm, item.ContentPackage);
                    if (featureDict.ContainsKey(feature.Name))
                    {
                        DebugConsole.ThrowError($"ContentPackage {item.ContentPackage.Name} contains a duplicate map feature with identifier {feature.Name}, skipping...");
                        continue;
                    }
                    featureDict.Add(feature.Name, feature);
                }
            }

            _IdentifierToFeature = featureDict;
            _Features = featureDict.Values.OrderBy(f => f.Name).ToList();
            Log.Debug($"Collected {_Features.Count} map features");
        }

        public static bool TryGetFeature(Identifier name, out MapFeature feature)
        {
            feature = null;
            if (!_IdentifierToFeature.ContainsKey(name)) return false;
            feature = _IdentifierToFeature[name];
            return true;
        }

        public override void OnLevelGenerate(LevelData levelData, bool mirror)
        {
            var data = levelData.MLC();
            if (data.MapFeatureData.Name.IsEmpty) return;
            if (!TryGetFeature(data.MapFeatureData.Name, out MapFeature feature))
            {
                Log.Error($"Tried to spawn non-existant map feature with identifier {data.MapFeatureData.Name}");
                return;
            }

            SubmarineFile file = ContentPackageManager.EnabledPackages.All.SelectMany(p => p.GetFiles<SubmarineFile>()).Where(f => f.Path.Value == feature.SubFile).FirstOrDefault();
            if (file == null)
            {
                Log.Error($"Failed to find submarine at path {feature.SubFile}");
                return;
            }

            // We need a custom placement thing for this
            MissionGenerationDirector.RequestSubmarine(new MissionGenerationDirector.SubmarineSpawnRequest()
            {
                AutoFill = true,
                File = file,
                IgnoreCrushDpeth = true,
                PlacementType = feature.PlacementType,
                AllowStealing = false,
                SpawnPosition = PositionType.Wreck,
                Callback = OnSubSpawned
            });

            static void OnSubSpawned(Submarine sub)
            {
                sub.PhysicsBody.FarseerBody.BodyType = FarseerPhysics.BodyType.Static;
                sub.TeamID = CharacterTeamType.FriendlyNPC;
                sub.ShowSonarMarker = false;
            }
        }

        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection)
        {
            RollForFeature(__instance, locationConnection);
        }

        public override void OnMapLoad(Map __instance)
        {
            if (!__instance.Connections.Any(c => !c.LevelData.MLC().MapFeatureData.Name.IsEmpty))
            {
                Log.Debug("Map has no map features, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];
                    RollForFeature(connection.LevelData, connection);
                }
            }
            else
            {
                Log.Debug("Map has map features");
            }
        }

        public override void OnPostRoundStart(LevelData levelData)
        {
            if (!TryGetFeature(levelData.MLC().MapFeatureData.Name, out MapFeature feature))
            {
                return;
            }
            if (GameMain.GameSession?.EventManager == null)
            {
                Log.Error("Event manager was null");
                return;
            }

            var rand = new MTRandom(GameMain.GameSession.EventManager.RandomSeed);
            var mapEvent = ToolBox.SelectWeightedRandom(feature.PossibleEvents, e => e.Commonness, rand);
            if (rand.NextDouble() > mapEvent.Probability) return;

            var eventPrefab = EventSet.GetAllEventPrefabs().Find(p => p.Identifier == mapEvent.EventIdentifier);
            if (eventPrefab == null)
            {
                DebugConsole.ThrowError($"Map Feature \"{feature.Name}\" failed to trigger an event (couldn't find an event with the identifier \"{mapEvent.EventIdentifier}\").",
                    contentPackage: feature.Package);
                return;
            }

            if (GameMain.GameSession?.EventManager != null)
            {
                var newEvent = eventPrefab.CreateInstance(GameMain.GameSession.EventManager.RandomSeed);
                GameMain.GameSession.EventManager.ActivateEvent(newEvent);
            }

        }

        void RollForFeature(LevelData data, LocationConnection connection)
        {
            // Check if there's already a map featue nearby
            if (connection.Locations.Any(l => _DisallowedLocations.Contains(l)))
            {
                return;
            }

            var rand = MLCUtils.GetRandomFromString(data.Seed);

            // Select feature to try and spawn
            MapFeature feature = ToolBox.SelectWeightedRandom(_Features, f => f.Commonness, rand);

            // Roll for spawn
            if (feature.Chance > rand.NextDouble())
            {
                data.MLC().MapFeatureData.Name = feature.Name;
                data.MLC().MapFeatureData.Revealed = feature.Display.HideUntilRevealed;
                _DisallowedLocations.AddRange(connection.Locations);
            }
        }
    }

    internal class MapFeature
    {
        public MapFeature(XElement element, ContentPackage package)
        {
            Package = package;
            SubFile = element.GetAttributeContentPath("path", package);
            Name = element.GetAttributeIdentifier("identifier", "");
            SpawnLocation = element.GetAttributeEnum("spawnPosition", SpawnLocation.MainPath);
            PlacementType = element.GetAttributeEnum("placement", PlacementType.Bottom);
            Chance = element.GetAttributeFloat("chance", 0);
            Commonness = element.GetAttributeFloat("commonness", 0);
            Display = new MapFeatureDisplay(element.GetChildElement("Display"));
            PossibleEvents = new();
            foreach (var item in element.GetChildElements("ScriptedEvent"))
            {
                PossibleEvents.Add(new MapFeatureEvent(item));
            }
        }

        public ContentPackage Package { get; private set; }
        public ContentPath SubFile { get; private set; }
        public Identifier Name { get; private set; }
        public SpawnLocation SpawnLocation { get; private set; }
        public PlacementType PlacementType { get; private set; }
        public float Chance { get; private set; }
        public float Commonness { get; private set; }
        public MapFeatureDisplay Display { get; private set; }
        public List<MapFeatureEvent> PossibleEvents { get; private set; }

        public struct MapFeatureDisplay
        {
            public MapFeatureDisplay(XElement element)
            {
                Icon = element.GetAttributeString("icon", "");
                Tooltip = element.GetAttributeString("tooltip", "");
                HideUntilRevealed = element.GetAttributeBool("hideuntilrevealed", false);
            }
            public string Icon { get; private set; }
            public string Tooltip { get; private set; }
            public bool HideUntilRevealed { get; private set; }
        }

        public struct MapFeatureEvent
        {
            public MapFeatureEvent(XElement element)
            {
                Probability = element.GetAttributeFloat("probability", 0);
                Commonness = element.GetAttributeFloat("commonness", 0);
                EventIdentifier = element.GetAttributeIdentifier("identifier", "");
            }
            public float Probability { get; private set; }
            public float Commonness { get; private set; }
            public Identifier EventIdentifier { get; private set; }
        }
    }


    [Flags]
    public enum SpawnLocation
    {
        MainPath = 1,
        SidePath = 2,
        Cave = 4,
        Abyss = 8,
        AbyssIsland = 16
    }
}

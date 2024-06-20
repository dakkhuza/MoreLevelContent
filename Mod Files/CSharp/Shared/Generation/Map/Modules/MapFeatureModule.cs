using Barotrauma;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using MoreLevelContent.Shared.Utils;
using static Barotrauma.Level;
using MoreLevelContent.Shared.Data;
using System.Globalization;
using static MoreLevelContent.Shared.Generation.MissionGenerationDirector;
using Barotrauma.Items.Components;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class MapFeatureModule : MapModule
    {
        private static List<MapFeature> _Features = new();
        private static Dictionary<Identifier, MapFeature> _IdentifierToFeature = new();
        private List<Location> _DisallowedLocations;
        public static Submarine MapFeatureSub { get; private set; }
        public static Identifier CurrentMapFeature { get; private set; }

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
            if (name.IsEmpty) return false;
            if (!_IdentifierToFeature.ContainsKey(name))
            {
                DebugConsole.ThrowError($"No map feature found with identifier '{name}'");
                return false;
            }
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
                SpawnPosition = feature.SpawnLocation,
                Callback = OnSubSpawned
            });

            void OnSubSpawned(Submarine sub)
            {
                MapFeatureSub = sub;
                CurrentMapFeature = feature.Name;
                SubPlacementUtils.SetCrushDepth(sub, true);
                sub.PhysicsBody.FarseerBody.BodyType = FarseerPhysics.BodyType.Static;
                sub.TeamID = CharacterTeamType.FriendlyNPC;
                sub.Info.Type = SubmarineType.Outpost;
                sub.InDetectable = true;
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
            if (levelData == null) return;
            if (levelData.Type == LevelData.LevelType.Outpost) return;
            var data = levelData.MLC();
            if (data == null) return;

            if (!TryGetFeature(data.MapFeatureData.Name, out MapFeature feature))
            {
                return;
            }

            if (MapFeatureSub == null)
            {
                DebugConsole.ThrowError("MLC: This level calls for a map feature but no map feature sub was spawned!");
            }

            // Set allow stealing
            if (!feature.AllowStealing)
            {
                foreach (var item in MapFeatureSub.GetItems(true))
                {
                    if (item.Container?.Prefab.AllowStealingContainedItems ?? false) continue;
                    item.AllowStealing = false;
                    item.SpawnedInCurrentOutpost = true;
                }
            }

            if (GameMain.GameSession?.EventManager == null)
            {
                Log.Error("Event manager was null");
                return;
            }

            if (feature.PossibleEvents.Count == 0) return;
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
            
            int zoneIndex = connection.Locations[0].GetZoneIndex(GameMain.GameSession.Map);

            var validFeatures = _Features.Where(f => f.CommonnessPerZone.ContainsKey(zoneIndex));
            if (!validFeatures.Any()) return;
            // Select feature to try and spawn
            MapFeature feature = ToolBox.SelectWeightedRandom(validFeatures, f => f.CommonnessPerZone[zoneIndex], rand);

            // Roll for spawn
            if (feature.Chance > rand.NextDouble())
            {
                data.MLC().MapFeatureData.Name = feature.Name;
                data.MLC().MapFeatureData.Revealed = !feature.Display.HideUntilRevealed;
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
            SpawnLocation = element.GetAttributeEnum("spawnPosition", SubSpawnPosition.PathWall);
            PlacementType = element.GetAttributeEnum("placement", PlacementType.Bottom);
            Chance = element.GetAttributeFloat("chance", 0);
            string[] commonnessPerZoneStrs = element.GetAttributeStringArray("commonnessperzone", Array.Empty<string>());
            ParseCommonnessPerZone(commonnessPerZoneStrs);

            AllowStealing = element.GetAttributeBool("allowstealing", true);
            Display = new MapFeatureDisplay(element.GetChildElement("Display"), Name);
            PossibleEvents = new();
            foreach (var item in element.GetChildElements("ScriptedEvent"))
            {
                PossibleEvents.Add(new MapFeatureEvent(item));
            }
        }

        public ContentPackage Package { get; private set; }
        public ContentPath SubFile { get; private set; }
        public Identifier Name { get; private set; }
        public SubSpawnPosition SpawnLocation { get; private set; }
        public PlacementType PlacementType { get; private set; }
        public float Chance { get; private set; }
        public Dictionary<int, float> CommonnessPerZone { get; private set; }
        public bool AllowStealing { get; private set; }
        public MapFeatureDisplay Display { get; private set; }
        public List<MapFeatureEvent> PossibleEvents { get; private set; }

        public struct MapFeatureDisplay
        {
            public MapFeatureDisplay(XElement element, Identifier name)
            {
                Icon = element.GetAttributeString("icon", "");
                Tooltip = element.GetAttributeString("tooltip", "");
                HideUntilRevealed = element.GetAttributeBool("hideuntilrevealed", false);
                DisplayName = TextManager.Get($"mapfeature.{name}.name");
            }
            public string Icon { get; private set; }
            public string Tooltip { get; private set; }
            public bool HideUntilRevealed { get; private set; }
            public LocalizedString DisplayName { get; private set; }
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

        void ParseCommonnessPerZone(string[] array)
        {
            CommonnessPerZone = new();
            foreach (string commonnessPerZoneStr in array)
            {
                string[] splitCommonnessPerZone = commonnessPerZoneStr.Split(':');
                if (splitCommonnessPerZone.Length != 2 ||
                    !int.TryParse(splitCommonnessPerZone[0].Trim(), out int zoneIndex) ||
                    !float.TryParse(splitCommonnessPerZone[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float zoneCommonness))
                {
                    DebugConsole.ThrowError("Failed to read commonness values for map feature  \"" + Name + "\" - commonness should be given in the format \"zone1index: zone1commonness, zone2index: zone2commonness\"");
                    break;
                }
                CommonnessPerZone[zoneIndex] = zoneCommonness;
            }
        }

        void ParseMinPerSone(string[] array)
        {

        }
    }


    [Flags]
    public enum SpawnLocation
    {
        Wreck = 1,
        Cave = 2,
        Abyss = 4,
        AbyssIsland = 8
    }
}

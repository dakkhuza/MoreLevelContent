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
        protected override void InitProjSpecific()
        {
            // Build table of map features
            _Features.Clear();
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
            RollForFeature(__instance);
        }

        public override void OnMapLoad(Map __instance)
        {
            if (!__instance.Connections.Any(c => !c.LevelData.MLC().MapFeatureData.Name.IsEmpty))
            {
                Log.Debug("Map has no map features, adding some...");
                for (int i = 0; i < __instance.Connections.Count; i++)
                {
                    var connection = __instance.Connections[i];
                    RollForFeature(connection.LevelData);
                }
            }
            else
            {
                Log.Debug("Map has map features");
            }
        }

        void RollForFeature(LevelData data)
        {
            var rand = MLCUtils.GetRandomFromString(data.Seed);

            // Select feature to try and spawn
            MapFeature feature = ToolBox.SelectWeightedRandom(_Features, f => f.Commonness, rand);

            // Roll for spawn
            if (feature.Chance > rand.NextDouble())
            {
                data.MLC().MapFeatureData.Name = feature.Name;
                data.MLC().MapFeatureData.Revealed = feature.Display.HideUntilRevealed;
                Log.Debug($"Added feature {feature.Name}");
            }
        }
    }

    internal class MapFeature
    {
        public MapFeature(XElement element, ContentPackage package)
        {
            SubFile = element.GetAttributeContentPath("path", package);
            Name = element.GetAttributeIdentifier("identifier", "");
            SpawnLocation = element.GetAttributeEnum("spawnPosition", SpawnLocation.MainPath);
            PlacementType = element.GetAttributeEnum("placement", PlacementType.Bottom);
            Chance = element.GetAttributeFloat("chance", 0);
            Commonness = element.GetAttributeFloat("commonness", 0);
            Display = new MapFeatureDisplay(element.GetChildElement("Display"));
        }

        public ContentPath SubFile;
        public Identifier Name;
        public SpawnLocation SpawnLocation;
        public PlacementType PlacementType;
        public float Chance;
        public float Commonness;
        public MapFeatureDisplay Display;

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

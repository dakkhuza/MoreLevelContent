using Barotrauma;
using Microsoft.CodeAnalysis;
using MoreLevelContent.Shared.Generation;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace MoreLevelContent.Shared.Data
{
    public class LevelData_MLCData : DataBase
    {
        [AttributeSaveData(false)]
        public bool HasBeaconConstruction;

        [AttributeSaveData(false)]
        public bool HasDistress;

        [AttributeSaveData(7)]
        public int DistressStepsLeft;

        [AttributeSaveData(false)]
        public bool HasPirateActivity;

        [AttributeSaveData(false)]
        public bool HasBlackMarket;

        [AttributeSaveData(RelayStationStatus.None)]
        public RelayStationStatus RelayStationStatus;

        [AttributeSaveData(false)]
        public bool HasLostCargo;

        [AttributeSaveData(4)]
        public int CargoStepsLeft;

        [AttributeSaveData(0)]
        public int RequestedU;

        [AttributeSaveData(0)]
        public int RequestedS;

        [AttributeSaveData(0)]
        public int RequestedE;

        [AttributeSaveData(TriangulationTarget.None)]
        public TriangulationTarget TriangulationTarget;

        public MapFeatureData MapFeatureData;

        internal PirateData PirateData;

        public bool HasRelayStation => RelayStationStatus != RelayStationStatus.None;

        public LocalizedString GetRequestedSupplies()
        {
            List<LocalizedString> requestedSuppliesList = new();

            // Utility
            if (RequestedU > 0)
            {
                requestedSuppliesList.Add(TextManager.GetWithVariable("mlc.beaconconstutility", "[count]", RequestedU.ToString()));
            }

            // Structural
            if (RequestedS > 0)
            {
                requestedSuppliesList.Add(TextManager.GetWithVariable("mlc.beaconconststructural", "[count]", RequestedS.ToString()));
            }

            // Electrical
            if (RequestedE > 0)
            {
                requestedSuppliesList.Add(TextManager.GetWithVariable("mlc.beaconconstelectrical", "[count]", RequestedE.ToString()));
            }
            switch (requestedSuppliesList.Count)
            {
                case 1:
                    return TextManager.GetWithVariable("mlc.beaconconstone", "[supply1]", requestedSuppliesList[0]);
                case 2:
                    return TextManager.GetWithVariables("mlc.beaconconsttwo", ("[supply1]", requestedSuppliesList[0]), ("[supply2]", requestedSuppliesList[1]));
                case 3:
                    return TextManager.GetWithVariables("mlc.beaconconstthree", ("[supply1]", requestedSuppliesList[0]), ("[supply2]", requestedSuppliesList[1]), ("[supply3]", requestedSuppliesList[2]));
                default:
                    Log.Error($"Invalid amount of requested supplies {requestedSuppliesList.Count}");
                    return null;
            }
        }

        protected override void LoadSpecific(XElement saveFile)
        {
            var pirateData = saveFile.GetChildElement("PirateData");
            if (pirateData != null)
            {
                PirateData = new PirateData()
                {
                    Status = pirateData.GetAttributeEnum("status", PirateOutpostStatus.None),
                    Difficulty = pirateData.GetAttributeFloat("difficulty", 0),
                    Revealed = pirateData.GetAttributeBool("revealed", false)
                };
            }
            var mapFeatureData = saveFile.GetChildElement("MapFeatureData");
            if (mapFeatureData != null)
            {
                MapFeatureData = new MapFeatureData()
                {
                    Name = mapFeatureData.GetAttributeIdentifier("name", null),
                    Revealed = mapFeatureData.GetAttributeBool("revealed", false)
                };
                if (MapFeatureModule.TryGetFeature(MapFeatureData.Name, out MapFeature feature))
                {
                    MapFeatureData.Feature = feature;
                }
            }
        }

        protected override void SaveSpecific(XElement saveFile)
        {
            var pirateData = new XElement("PirateData",
                new XAttribute("status", PirateData.Status),
                new XAttribute("difficulty", PirateData.Difficulty),
                new XAttribute("revealed", PirateData.Revealed));

            var mapFeatureData = new XElement("MapFeatureData",
                new XAttribute("name", MapFeatureData.Name),
                new XAttribute("revealed", MapFeatureData.Revealed));

            saveFile.Add(pirateData);
            saveFile.Add(mapFeatureData);
        }
    }

    public class MapFeatureData
    {
        public Identifier Name;
        public bool Revealed;
        internal MapFeature Feature;
        public bool HasFeature => Feature != null;
    }

    public static partial class MLCData
    {
        private static readonly ConditionalWeakTable<LevelData, LevelData_MLCData> levelData_data = new();

        internal static LevelData_MLCData MLC(this LevelData levelData) => levelData_data.GetOrCreateValue(levelData);

        internal static void AddData(this LevelData levelData, LevelData_MLCData additional)
        {
            try
            {
                levelData_data.Add(levelData, additional);
            } catch(Exception e) { Log.Error(e.ToString()); }
        }
    }

    public enum PirateOutpostStatus
    {
        None,
        Active,
        Destroyed,
        Husked
    }

    public enum RelayStationStatus
    {
        None,
        Inactive,
        Active
    }

    public enum TriangulationTarget
    {
        None,
        MapFeature,
        PirateBase,
        Treasure
    }

    internal class PirateData
    {
        public PirateData()
        {
            Status = PirateOutpostStatus.None;
            Difficulty = 0;
            Revealed = false;
        }

        public PirateData(PirateSpawnData spawnData)
        {
            Difficulty = 0;
            Status = PirateOutpostStatus.None;
            Revealed = false;

            if (spawnData.WillSpawn)
            {
                Status = PirateOutpostStatus.Active;
                Difficulty = spawnData.PirateDifficulty;

                if (spawnData.Husked)
                {
                    Status = PirateOutpostStatus.Husked;
                }
            }
        }

        public PirateOutpostStatus Status;
        public float Difficulty;
        public bool Revealed;

        public bool HasPirateBase => Status != PirateOutpostStatus.None;
    }
}

using Barotrauma;
using MoreLevelContent.Shared.Generation.Pirate;
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

        public PirateData PirateData;

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
        Destroyed
    }

    public struct PirateData
    {
        public PirateData()
        {
            Status = PirateOutpostStatus.None;
            Difficulty = 0;
            Husked = false;
        }

        public PirateData(PirateSpawnData spawnData)
        {
            Husked = false;
            Difficulty = 0;
            Status = PirateOutpostStatus.None;

            if (spawnData.WillSpawn)
            {
                Status = PirateOutpostStatus.Active;
                Difficulty = spawnData.PirateDifficulty;

                if (spawnData.Husked)
                {
                    Status = PirateOutpostStatus.Destroyed;
                    Husked = true;
                }
            }
        }

        public PirateOutpostStatus Status;
        public float Difficulty;
        public bool Husked;

        public bool HasPirateOutpost => Status == PirateOutpostStatus.Destroyed || Status == PirateOutpostStatus.Active;
    }
}

using Barotrauma;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MoreLevelContent.Shared.Data
{
    public class LevelData_MLCData : DataBase
    {
        [SaveData(false)]
        public bool HasBeaconConstruction;

        [SaveData(false)]
        public bool HasDistress;

        [SaveData(7)]
        public int DistressStepsLeft;

        [SaveData(false)]
        public bool HasPirateActivity;

        [SaveData(false)]
        public bool HasBlackMarket;

        [SaveData(false)]
        public bool HasLostCargo;

        [SaveData(4)]
        public int CargoStepsLeft;

        [SaveData(0)]
        public int RequestedU;

        [SaveData(0)]
        public int RequestedS;

        [SaveData(0)]
        public int RequestedE;

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
}

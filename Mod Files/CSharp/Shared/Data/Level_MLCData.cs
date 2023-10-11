using Barotrauma;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MoreLevelContent.Shared.Data
{
    class Level_MLCData : DataBase
    {
        public Submarine BeaconConstructionStation;
        public Item DropOffPoint;

        public bool CheckSuppliesDelivered()
        {
            if (DropOffPoint == null)
            {
                Log.Error("No drop off point specified!!");
                return false;
            }
            int utilityCount = DropOffPoint.ContainedItems.Where(it => it.Tags.Contains("supply_utility")).Count();
            int structualCount = DropOffPoint.ContainedItems.Where(it => it.Tags.Contains("supply_structural")).Count();
            int electricalCount = DropOffPoint.ContainedItems.Where(it => it.Tags.Contains("supply_electrical")).Count();

            int requestedUtility = Level.Loaded.LevelData.MLC().RequestedU;
            int requestedStructual = Level.Loaded.LevelData.MLC().RequestedS;
            int requestedElectrical = Level.Loaded.LevelData.MLC().RequestedE;

            //Log.InternalDebug($"Utility: {utilityCount} / {requestedUtility}, Structual: {structualCount} / {requestedStructual}, Electrical: {electricalCount} / {requestedElectrical}");
            return 
                utilityCount >= requestedUtility && // electrical
                structualCount >= requestedStructual && // structural
                electricalCount >= requestedElectrical;   // electrical
        }
    }

    public static partial class MLCData
    {
        private static readonly ConditionalWeakTable<Level, Level_MLCData> level_data = new();

        internal static Level_MLCData MLC(this Level levelData) => level_data.GetOrCreateValue(levelData);

        internal static void AddData(this Level levelData, Level_MLCData additional)
        {
            try
            {
                level_data.Add(levelData, additional);
            }
            catch (Exception e) { Log.Error(e.ToString()); }
        }

    }
}

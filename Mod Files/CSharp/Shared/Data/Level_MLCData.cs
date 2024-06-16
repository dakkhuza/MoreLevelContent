using Barotrauma;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MoreLevelContent.Shared.Data
{
    class Level_MLCData : DataBase
    {
        public ContentFile RelayStationFile;
        public Submarine BeaconConstructionStation;
        public Submarine RelayStation;
        public Item DropOffPoint;

        public bool CheckSuppliesDelivered()
        {
            if (DropOffPoint == null)
            {
                Log.Error("No drop off point specified!!");
                return false;
            }
            int uCount = DropOffPoint.ContainedItems.Where(it => it.Tags.Contains("supply_utility")).Count();
            int sCount = DropOffPoint.ContainedItems.Where(it => it.Tags.Contains("supply_structural")).Count();
            int eCount = DropOffPoint.ContainedItems.Where(it => it.Tags.Contains("supply_electrical")).Count();
            return 
                uCount >= Level.Loaded.LevelData.MLC().RequestedU && // electrical
                sCount >= Level.Loaded.LevelData.MLC().RequestedS && // structural
                eCount >= Level.Loaded.LevelData.MLC().RequestedE;   // electrical
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

using Barotrauma;
using Barotrauma.Extensions;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MoreLevelContent.Shared.Store
{
    public class BeaconConstStore : StoreBase<BeaconConstStore>
    {
        private List<OutpostModuleFile> ConstBeacons = new();
        public override void Setup() => HasContent = FindConstBeacons();

        internal OutpostModuleFile GetBeaconForLevel()
        {
            Random rand = MLCUtils.GetLevelRandom();
            return ConstBeacons.GetRandom(rand);
        }

        bool FindConstBeacons()
        {
            ConstBeacons = GetOutpostModuleFilesWithLocation("mlc_BeaconConstruction");
            return ConstBeacons.Count() > 0;
            // ConstBeacons.Sort();
        }
    }
}

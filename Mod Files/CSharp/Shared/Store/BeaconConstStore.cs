using Barotrauma;
using MoreLevelContent.Shared.Generation;
using System.Collections.Generic;
using System.Linq;

namespace MoreLevelContent.Shared.Store
{
    public class BeaconConstStore : StoreBase<BeaconConstStore>
    {
        private List<OutpostModuleFile> ConstBeacons = new();
        public override void Setup()
        {
            HasContent = FindConstBeacons();
        }

        internal OutpostModuleFile GetBeacon()
        {
            return ConstBeacons[0];
        }

        bool FindConstBeacons()
        {
            ConstBeacons = GetOutpostModuleFilesWithLocation("mlc_BeaconConstruction");
            return ConstBeacons.Count() > 0;
            // ConstBeacons.Sort();
        }
    }
}

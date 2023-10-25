using Barotrauma;
using MoreLevelContent.Missions;
using MoreLevelContent.Shared.Data;
using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;

namespace MoreLevelContent.Shared.Generation
{
    internal partial class LostCargoMapModule : MapModule
    {
        public override void OnAddExtraMissions(CampaignMode __instance, LevelData levelData) { }
        public override void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection)
        {
            var levelData = locationConnection.LevelData.MLC();
            levelData.HasLostCargo = Rand.Value() > 0.5;
        }
        public override void OnLevelDataLoad(LevelData __instance, XElement element) { }
        public override void OnLevelDataSave(LevelData __instance, XElement parentElement) { }
        public override void OnNewMap(Map __instance) { }
        public override void OnProgressWorld(Map __instance) { }
        protected override void InitProjSpecific() { }
    }
}

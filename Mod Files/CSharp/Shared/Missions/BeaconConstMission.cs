using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;

namespace MoreLevelContent.Missions
{
    partial class BeaconConstMission : Mission
    {
        private readonly LocalizedString sonarLabel;
        private readonly int PriceUtility;
        private readonly int PriceStructure;
        private readonly int PriceElectric;
        public BeaconConstMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            sonarLabel = TextManager.Get("beaconconsonarlabel");
            var supplyCosts = prefab.ConfigElement.GetChildElement("supplycosts");
            PriceUtility = GetPrice("supply_utility");
            PriceStructure = GetPrice("supply_structural");
            PriceElectric = GetPrice("supply_electrical");

            string rewardText = $"‖color:gui.orange‖{string.Format(CultureInfo.InvariantCulture, "{0:N0}", GetReward(sub))}‖end‖";
            description = descriptionWithoutReward.Replace("[reward]", rewardText);

            int GetPrice(string supplyType) => supplyCosts.GetChildElement(supplyType).GetAttributeInt("price", 0);
        }

        // public override LocalizedString SonarLabel => base.SonarLabel.IsNullOrEmpty() ? sonarLabel : base.SonarLabel;
        public override int Reward => GetReward();
        public override int GetBaseReward(Submarine sub) => GetReward();

        private int GetReward()
        {
            LocationConnection connection = Locations[0].Connections.Find(lc => lc.Locations.Contains(Locations[1]));
            var levelData = connection.LevelData.MLC();
            int reward = (int)(((PriceUtility * levelData.RequestedU) +
            (PriceStructure * levelData.RequestedS) +
            (PriceElectric * levelData.RequestedE)) * 2.5);
            return reward;
        }

        protected override void StartMissionSpecific(Level level) => description = description.Replace("[requestedsupplies]", level.LevelData.MLC().GetRequestedSupplies());

        public override IEnumerable<(LocalizedString Label, Vector2 Position)> SonarLabels
        {
            get
            {
                if (level.MLC().BeaconConstructionStation == null)
                {
                    yield break;
                }
                Vector2 worldPos = level.MLC().BeaconConstructionStation.WorldPosition;
                yield return (Prefab.SonarLabel.IsNullOrEmpty() ? sonarLabel : Prefab.SonarLabel, worldPos);
            }
        }

        protected override void UpdateMissionSpecific(float deltaTime)
        {
            if (IsClient) { return; }
            if (State == 0 && level.MLC().CheckSuppliesDelivered())
            {
                State = 1;
            }
        }

        protected override void EndMissionSpecific(bool completed)
        {
            if (completed && level.LevelData != null)
            {
                level.LevelData.IsBeaconActive = true;
                level.LevelData.HasBeaconStation = true;
                level.LevelData.MLC().HasBeaconConstruction = false;
            }
        }

        public override void AdjustLevelData(LevelData levelData) => levelData.MLC().HasBeaconConstruction = true;
        protected override bool DetermineCompleted() => level.MLC().CheckSuppliesDelivered();
    }
}

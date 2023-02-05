
using Barotrauma;
using MoreLevelContent.Missions;
using System;
using System.Collections.Generic;

namespace MoreLevelContent.Shared.Content
{
    public class CustomMissions
    {
        public static readonly Dictionary<CustomMissionType, Type> MissionDefs = new()
        {
            { CustomMissionType.BeaconConstruction, typeof(BeaconConstMission) },
            { CustomMissionType.DistressEscort, typeof(DistressEscortMission) },
            { CustomMissionType.DistressSubmarine, typeof(DistressSubmarineMission) },
            { CustomMissionType.DistressGhostship, typeof(DistressGhostshipMission) },
            //{ CustomMissionType.DistressOutpost, typeof(DistressOutpostMission) }
        };
    }

    public enum CustomMissionType
    {
        BeaconConstruction,
        DistressEscort,
        DistressSubmarine,
        DistressGhostship
        //DistressOutpost
    }
}

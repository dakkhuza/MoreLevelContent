
using Barotrauma;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MoreLevelContent
{
    /// <summary>
    /// Changes what map feature the current level has.
    /// </summary>
    internal class RevealMapFeatureAction : EventAction
    {
        [Serialize("", IsPropertySaveable.Yes, description: "The Identifier of the map feature to search for.")]
        public Identifier MapFeatureIdentifier { get; set; }

        private bool isFinished = false;


        public RevealMapFeatureAction(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
            if (MapFeatureIdentifier.IsEmpty)
            {
                DebugConsole.ThrowError($"Error in event \"{parentEvent.Prefab.Identifier}\": MapFeatureIdentifier has not been configured.",
                    contentPackage: element.ContentPackage);
            }
        }

        public override void Update(float deltaTime)
        {
            if (GameMain.GameSession.GameMode is CampaignMode campaign)
            {
                LocationConnection featureLocation;
                try
                {
                    featureLocation = FindConnectionWithMapFeature(MapFeatureIdentifier);
                } catch(Exception e)
                {
                    isFinished = true;
                    Log.Error($"RevealMapFeatureAction crashed! {e.Message}");
                    return;
                }

                if (featureLocation != null)
                {
                    featureLocation.LevelData.MLC().MapFeatureData.Revealed = true;

                    // Probably have to do some syncing here, maybe not
                    if (campaign is MultiPlayerCampaign mpCampaign)
                    {
                        mpCampaign.IncrementLastUpdateIdForFlag(MultiPlayerCampaign.NetFlags.MapAndMissions);
                    }
                    foreach (var location in featureLocation.Locations)
                    {
                        campaign.Map.Discover(location, checkTalents: false);
                    }
                }
            }

            isFinished = true;
        }

        private LocationConnection FindConnectionWithMapFeature(Identifier name)
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign) return null;

            var start = Level.Loaded.StartLocation;
            var end = Level.Loaded.EndLocation;
            var currentConnection = start.Connections.Where(c => c.OtherLocation(start) == end).First();
            HashSet<LocationConnection> checkedConnections = new HashSet<LocationConnection>();
            HashSet<LocationConnection> pendingConnections = new HashSet<LocationConnection>() { currentConnection };

            do
            {
                List<LocationConnection> currentConnections = pendingConnections.ToList();
                pendingConnections.Clear();
                foreach (var connection in currentConnections)
                {
                    checkedConnections.Add(connection);
                    var data = connection.LevelData.MLC();
                    if (data == null)
                    {
                        Log.Error("Missing data");
                        continue;
                    }
                    MapFeatureData feature = data.MapFeatureData;
                    if (feature.Name == name && !feature.Revealed)
                    {
                        return connection;
                    }
                    else
                    {
                        foreach (Location location in connection.Locations)
                        {
                            foreach (var item in location.Connections)
                            {
                                if (checkedConnections.Contains(item)) { continue;  }
                                pendingConnections.Add(item);
                            }
                        }
                    }
                }


            } while (pendingConnections.Any());


            return null;
        }

        public override bool IsFinished(ref string goToLabel) => isFinished;
        public override void Reset() => isFinished = false;
        public override string ToDebugString()
        {
            return $"{ToolBox.GetDebugSymbol(isFinished)} {nameof(RevealMapFeatureAction)} -> ({(MapFeatureIdentifier)})";
        }

    }
}

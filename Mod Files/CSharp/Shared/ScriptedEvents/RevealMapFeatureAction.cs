
using Barotrauma;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.XML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MoreLevelContent
{
    /// <summary>
    /// Finds and reveals a hidden map feature.
    /// </summary>
    [InjectScriptedEvent]
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
                LocationConnection featureConnection;
                try
                {
                    featureConnection = FindConnectionWithMapFeature(MapFeatureIdentifier);
                } catch(Exception e)
                {
                    isFinished = true;
                    Log.Error($"RevealMapFeatureAction crashed! {e.Message}");
                    return;
                }

                if (featureConnection != null)
                {
                    MapFeatureModule.TryGetFeature(featureConnection.LevelData.MLC().MapFeatureData.Name, out MapFeature feature);

                    // Probably have to do some syncing here, maybe not
                    if (campaign is MultiPlayerCampaign mpCampaign)
                    {
                        mpCampaign.IncrementLastUpdateIdForFlag(MultiPlayerCampaign.NetFlags.MapAndMissions);
                    }

#if CLIENT
                    ShowNotification(feature, featureConnection);
#else
                    MapDirector.Instance.NotifyMapFeatureRevealed(featureConnection, featureConnection.LevelData.MLC().MapFeatureData);
                    featureConnection.LevelData.MLC().MapFeatureData.Revealed = true;
#endif

                }
            }

            isFinished = true;
        }

#if CLIENT
        public static void ShowNotification(MapFeature feature, LocationConnection featureConnection)
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign) return;
            _ = new GUIMessageBox(TextManager.GetWithVariable("mapfeature.revealed.header", "[feature]", feature.Display.DisplayName), TextManager.Get("mapfeature.revealed.description"),
                Array.Empty<LocalizedString>(), type: GUIMessageBox.Type.InGame, iconStyle: feature.Display.Icon, relativeSize: new Vector2(0.2f, 0.06f), minSize: new Point(64, 74));
            featureConnection.LevelData.MLC().MapFeatureData.Revealed = true;
        }
#endif

        private LocationConnection FindConnectionWithMapFeature(Identifier name)
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign) return null;

            LocationConnection currentConnection;
            if (Level.Loaded.Type == LevelData.LevelType.LocationConnection)
            {
                var start = Level.Loaded.StartLocation;
                var end = Level.Loaded.EndLocation;
                currentConnection = start.Connections.Where(c => c.OtherLocation(start) == end).FirstOrDefault();
            } else
            {
                currentConnection = campaign.CurrentLocation.Connections.FirstOrDefault();
            }

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

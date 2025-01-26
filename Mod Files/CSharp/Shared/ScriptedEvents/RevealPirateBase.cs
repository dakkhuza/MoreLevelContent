
using Barotrauma;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Generation.Pirate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

namespace MoreLevelContent
{
    /// <summary>
    /// Changes what map feature the current level has.
    /// </summary>
    internal class RevealPirateBase : BinaryOptionAction
    {
        public RevealPirateBase(ScriptedEvent parentEvent, ContentXElement element) : base(parentEvent, element)
        {
            if (GameMain.GameSession.GameMode is CampaignMode campaign)
            {
                LocationConnection pirateBaseConnection;
                try
                {
                    pirateBaseConnection = FindPirateBase();
                }
                catch (Exception e)
                {
                    Log.Error($"RevealMapFeatureAction crashed! {e.Message}");
                    return;
                }

                if (pirateBaseConnection != null)
                {
                    var pirateData = pirateBaseConnection.LevelData.MLC().PirateData;

                    // Probably have to do some syncing here, maybe not
                    if (campaign is MultiPlayerCampaign mpCampaign)
                    {
                        mpCampaign.IncrementLastUpdateIdForFlag(MultiPlayerCampaign.NetFlags.MapAndMissions);
                    }

#if CLIENT
                    ShowNotification(pirateBaseConnection);
#else
                    MapDirector.Instance.NotifyMapFeatureRevealed(pirateBaseConnection, pirateBaseConnection.LevelData.MLC().MapFeatureData);
                    pirateBaseConnection.LevelData.MLC().MapFeatureData.Revealed = true;
#endif

                }
            }
        }

        private LocationConnection FindPirateBase()
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign) return null;

            LocationConnection startSearchConnection;
            if (Level.Loaded.Type == LevelData.LevelType.LocationConnection)
            {
                var start = Level.Loaded.StartLocation;
                var end = Level.Loaded.EndLocation;
                startSearchConnection = start.Connections.Where(c => c.OtherLocation(start) == end).FirstOrDefault();
            }
            else
            {
                startSearchConnection = campaign.CurrentLocation.Connections.FirstOrDefault();
            }

            HashSet<LocationConnection> checkedConnections = new HashSet<LocationConnection>();
            HashSet<LocationConnection> pendingConnections = new HashSet<LocationConnection>() { startSearchConnection };

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
                    var pirateData = data.PirateData;
                    // Don't use the current connection, only look for active and not revealed bases
                    if (startSearchConnection != connection && pirateData.Status == PirateOutpostStatus.Active && !pirateData.Revealed)
                    {
                        Log.Debug("Found connection with pirate base");
                        return connection;
                    }
                    else
                    {
                        foreach (Location location in connection.Locations)
                        {
                            foreach (var item in location.Connections)
                            {
                                if (checkedConnections.Contains(item)) { continue; }
                                pendingConnections.Add(item);
                            }
                        }
                    }
                }


            } while (pendingConnections.Any());


            return null;
        }

        protected override bool? DetermineSuccess()
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign) return false;

            try
            {
                LocationConnection connection = FindPirateBase();
                if (connection != null)
                {
                    if (campaign is MultiPlayerCampaign mpCampaign)
                    {
                        mpCampaign.IncrementLastUpdateIdForFlag(MultiPlayerCampaign.NetFlags.MapAndMissions);
                    }
#if CLIENT
                    ShowNotification(connection);
#else
                    var data = connection.LevelData.MLC().PirateData;
                    data.Revealed = true;
                    PirateOutpostDirector.UpdateStatus(data, connection);
#endif

                    return true;
                }
            }
            catch(Exception e)
            {
                Log.Error($"RevealMapFeatureAction crashed! {e.Message}");
            }



            return false;
        }

#if CLIENT
        public static void ShowNotification(LocationConnection connection)
        {
            if (GameMain.GameSession.GameMode is not CampaignMode) return;
            _ = new GUIMessageBox
                (TextManager.Get("mapupdate.generic.header"), 
                TextManager.GetWithVariables("mapupdate.piratebase.revealed",
                ("[location1]", $"‖color:gui.orange‖{connection.Locations[0]}‖end‖"),
                ("[location2]", $"‖color:gui.orange‖{connection.Locations[1]}‖end‖")),
                Array.Empty<LocalizedString>(), type: GUIMessageBox.Type.InGame, iconStyle: "PirateBase", relativeSize: new Vector2(0.2f, 0.06f), minSize: new Point(64, 74));
        }
#endif

    }
}

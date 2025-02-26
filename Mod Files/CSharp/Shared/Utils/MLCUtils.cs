using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using FarseerPhysics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Voronoi2;
using static Barotrauma.Level;

namespace MoreLevelContent.Shared.Utils
{
    public static class MLCUtils
    {
        internal static string GetRandomTag(string baseTag)
        {
            int maxIndex = 1;
            while (TextManager.ContainsTag(baseTag + maxIndex))
            {
                maxIndex++;
            }
            return "mlc.lostcargo.tooslow" + Rand.Range(0, maxIndex);
        }

        internal static string GetRandomTag(string baseTag, LevelData data)
        {
            Random rand = new MTRandom(ToolBox.StringToInt(data.Seed));
            int maxIndex = 1;
            while (TextManager.ContainsTag(baseTag + maxIndex))
            {
                maxIndex++;
            }
            return baseTag + rand.Next(0, maxIndex);
        }


        internal static Vector2 PositionItemOnEdge(Item target, GraphEdge edge, float height, bool setRotation = false)
        {
            Vector2 dir = Vector2.Normalize(edge.GetNormal(edge.Cell1 ?? edge.Cell2));
            float angle = Angle(dir) - 90;
            Vector2 pos = ConvertUnits.ToSimUnits(edge.Center + (edge.GetNormal(edge.Cell1 ?? edge.Cell2) * height));
            SetItemPosition(target, pos, setRotation ? MathHelper.ToRadians(angle) : 0);
            target.Rotation = -angle;
            return dir;
        }
        internal static void SetItemPosition(Item target, Vector2 simPos, float rot) => target.SetTransform(simPos - (target.Submarine?.SimPosition ?? Vector2.Zero), rot, false);
        internal static float Angle(Vector2 dir) => (float)(MathUtils.VectorToAngle(dir) * 180 / Math.PI);

        internal static Random GetLevelRandom()
        {
            if (Level.Loaded == null)
            {
                Log.Error("Level was null when we tried to get a random instance!");
                return null;
            }

            return new MTRandom(ToolBox.StringToInt(Level.Loaded.LevelData.Seed));
        }

        internal static Location FindUnlockLocation(FindLocationInfo info)
        {
            if (GameMain.GameSession.GameMode is not CampaignMode campaign)
            {
                Log.Warn("Not campaign mode, can't find location");
                return null;
            }
            if (info.MinDistance <= 1)
            {
                return campaign.Map.CurrentLocation;
            }

            var currentLocation = campaign.Map.CurrentLocation;
            int distance = 0;
            HashSet<Location> checkedLocations = new HashSet<Location>();
            HashSet<Location> pendingLocations = new HashSet<Location>() { currentLocation };
            do
            {
                List<Location> currentLocations = pendingLocations.ToList();
                pendingLocations.Clear();
                foreach (var location in currentLocations)
                {
                    checkedLocations.Add(location);
                    if (IsLocationValid(currentLocation, location, distance, info))
                    {
                        return location;
                    }
                    else
                    {
                        foreach (LocationConnection connection in location.Connections)
                        {
                            var otherLocation = connection.OtherLocation(location);
                            if (checkedLocations.Contains(otherLocation)) { continue; }
                            pendingLocations.Add(otherLocation);
                        }
                    }
                }
                distance++;
            } while (pendingLocations.Any());

            return null;
        }

        internal static bool IsLocationValid(Location currentLocation, Location location, int distance, FindLocationInfo info)
        {
            if (!info.RequiredFaction.IsEmpty)
            {
                if (location.Faction?.Prefab.Identifier != info.RequiredFaction &&
                    location.SecondaryFaction?.Prefab.Identifier != info.RequiredFaction)
                {
                    return false;
                }
            }
            if (info.AllowedLocationTypes != null && info.AllowedLocationTypes.Count() > 0 && !info.AllowedLocationTypes.Contains(location.Type.Identifier) && !(location.HasOutpost() && info.AllowedLocationTypes.Contains(Tags.AnyOutpost)))
            {
                return false;
            }
            if (distance < info.MinDistance)
            {
                return false;
            }
            if (info.MustBeFurtherOnMap && location.MapPosition.X < currentLocation.MapPosition.X)
            {
                return false;
            }
            if (info.MustBeHidden && !location.Discovered)
            {
                return false;
            }
            return true;
        }

        internal struct FindLocationInfo
        {
            public int MinDistance;
            public int MaxDistance;
            public bool MustBeFurtherOnMap;
            public IEnumerable<Identifier> AllowedLocationTypes;
            public Identifier RequiredFaction;
            public bool MustBeHidden;
        }

        internal static Random GetRandomFromString(string seed)
        {
            return new MTRandom(ToolBox.StringToInt(seed));
        }
    }
}

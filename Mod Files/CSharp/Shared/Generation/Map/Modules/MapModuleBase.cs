using Barotrauma;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Xml.Linq;

namespace MoreLevelContent.Shared.Generation
{
    internal abstract class MapModule
    {
        public MapModule() => InitProjSpecific();
        protected MapDirector Instance => MapDirector.Instance;

        protected abstract void InitProjSpecific();

        protected static bool TryGetMissionByTag(string tag, LevelData data, out MissionPrefab missionPrefab)
        {
            var orderedMissions = MissionPrefab.Prefabs.Where(m => m.Tags.Contains(tag)).OrderBy(m => m.UintIdentifier);
            Random rand = new MTRandom(ToolBox.StringToInt(data.Seed));
            missionPrefab = ToolBox.SelectWeightedRandom(orderedMissions, p => p.Commonness, rand);
            return missionPrefab != null;
        }

        protected LocationConnection WalkConnection(Location start, Random rand, int preferedWalkDistance)
        {
            // Since we do a connection step at the end of the process, there's one step implict in every walk
            // so we subtract a step here
            int actualWalkDist = preferedWalkDistance - 1;
            if (actualWalkDist <= 0)
            {
                return GetConnectionWeighted(start, rand);
            }
            Location location = WalkLocation(start, rand, actualWalkDist);
            return GetConnectionWeighted(location, rand);
        }

        protected Location WalkLocation(Location start, Random rand, int preferedWalkDistance, LocationConnection from = null)
        {
            var filteredConnections = start.Connections.Where(c => c != from);
            if (!filteredConnections.Any())
            {
                return start;
            }

            LocationConnection connectionToTravel = ToolBox.SelectWeightedRandom(
                filteredConnections.ToList(),
                filteredConnections.Select(c => GetConnectionWeight(start, c)).ToList(),
                rand);

            Location walkedLocation = connectionToTravel.OtherLocation(start);
            preferedWalkDistance--;

            // if we haven't walked our wanted dist or 
            if (preferedWalkDistance > 0) walkedLocation = WalkLocation(walkedLocation, rand, preferedWalkDistance);
            return walkedLocation;
        }

        static LocationConnection GetConnectionWeighted(Location location, Random rand)
        {
            LocationConnection connectionToTravel = ToolBox.SelectWeightedRandom(
                location.Connections,
                location.Connections.Select(c => GetConnectionWeight(location, c)).ToList(),
                rand);

            return connectionToTravel;
        }

        static float GetConnectionWeight(Location location, LocationConnection c)
        {

            // get the destination of this connection
            Location destination = c.OtherLocation(location);
            if (destination == null) { return 0; }
            float minWeight = 0.0001f;
            float lowWeight = 0.2f;
            float normalWeight = 1.0f;
            float maxWeight = 2.0f;

            // prefer connections we haven't passed through
            float weight = c.Passed ? lowWeight : normalWeight;

            if (location.Biome.AllowedZones.Contains(1))
            {
                // In the first biome, give a stronger preference for locations that are farther to the right)
                float diff = destination.MapPosition.X - location.MapPosition.X;
                if (diff < 0)
                {
                    weight *= 0.1f;
                }
                else
                {
                    float maxRelevantDiff = 300;
                    weight = MathHelper.Lerp(weight, maxWeight, MathUtils.InverseLerp(0, maxRelevantDiff, diff));
                }
            }
            else if (destination.MapPosition.X > location.MapPosition.X)
            {
                weight *= 2.0f;
            }

            if (destination.IsRadiated())
            {
                weight *= 0.001f;
            }

            // Prefer locations that have been revealed
            if (!destination.Discovered)
            {
                weight *= 0.5f;
            }

            return MathHelper.Clamp(weight, minWeight, maxWeight);
        }


        protected void SendChatUpdate(string msg)
        {
#if CLIENT
            if (GameMain.Client != null)
            {
                GameMain.Client.AddChatMessage(msg, Barotrauma.Networking.ChatMessageType.Default, TextManager.Get("mlc.navigationannouce").Value);
            }
            else
            {
                GameMain.GameSession?.GameMode.CrewManager.AddSinglePlayerChatMessage(
                    TextManager.Get("mlc.navigationannouce").Value,
                    msg,
                    Barotrauma.Networking.ChatMessageType.Default,
                    sender: null);
            }
#endif
        }

        public virtual void OnAddExtraMissions(CampaignMode __instance, LevelData levelData) { }
        public virtual void OnRoundStart(LevelData levelData) { }
        public virtual void OnLevelDataGenerate(LevelData __instance, LocationConnection locationConnection) { }
        public virtual void OnProgressWorld(Map __instance) { }
        public virtual void OnLevelDataLoad(LevelData __instance, XElement element) { }
        public virtual void OnLevelDataSave(LevelData __instance, XElement parentElement) { }
        public virtual void OnNewMap(Map __instance) { }
    }
}

using Barotrauma;
using System;
using System.Xml.Linq;

namespace MoreLevelContent.Shared.Generation
{
    internal abstract class MapModule
    {
        public MapModule() => InitProjSpecific();
        protected MapDirector Instance => MapDirector.Instance;

        protected abstract void InitProjSpecific();

        protected LocationConnection WalkConnection(Location start, Random rand, int preferedWalkDistance)
        {
            Location location = WalkLocation(start, rand, preferedWalkDistance);
            return location.Connections[rand.Next(0, location.Connections.Count - 1)];
        }

        protected Location WalkLocation(Location start, Random rand, int preferedWalkDistance)
        {
            int potentialConnections = start.Connections.Count - 1;
            int connectionIndex = rand.Next(0, potentialConnections);
            Location walkedLocation = start.Connections[connectionIndex].OtherLocation(start);
            preferedWalkDistance--;
            if (preferedWalkDistance > 0) walkedLocation = WalkLocation(walkedLocation, rand, preferedWalkDistance);
            return walkedLocation;
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
        public virtual void OnMapLoad(Map __instance) { }
        public virtual void OnMapGenerate(Map __instance) { }
    }
}

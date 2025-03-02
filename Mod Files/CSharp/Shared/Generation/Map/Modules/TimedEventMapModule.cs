﻿using Barotrauma;
using Barotrauma.MoreLevelContent.Config;
using Barotrauma.Networking;
using FarseerPhysics.Collision;
using Microsoft.Xna.Framework;
using MoreLevelContent.Networking;
using MoreLevelContent.Shared.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MoreLevelContent.Shared.Generation
{
    abstract partial class TimedEventMapModule : MapModule
    {
        public TimedEventMapModule()
        {
            InitProjSpecific();
        }

        public string ForcedMissionIdentifier = "";
        public bool ForceSpawnMission = false;

        // Networking
        protected abstract NetEvent EventCreated { get; }
        //protected abstract NetEvent EventUpdated { get; }

        // Text
        protected abstract string NewEventText { get; }
        //protected abstract string UpdatedEventText { get; }
        protected abstract string EventTag { get; }

        // Config
        protected abstract int MaxActiveEvents { get; }
        protected abstract float EventSpawnChance { get; }
        protected abstract int MinDistance { get; }
        protected abstract int MaxDistance { get; }
        protected abstract int MinEventDuration { get; }
        protected abstract int MaxEventDuration { get; }
        protected abstract bool ShouldSpawnEventAtStart { get; }
        protected bool SpawnedEventAtStart
        {
            get => GameMain.GameSession.Campaign.CampaignMetadata.GetBoolean($"{EventTag}SpawnedStart", false);
            set => GameMain.GameSession.Campaign.CampaignMetadata.SetValue($"{EventTag}SpawnedStart", value);
        }

        private readonly List<Mission> _internalMissionStore = new();

        public override void OnProgressWorld(Map __instance)
        {
            foreach (LocationConnection connection in __instance.Connections)
            {
                // skip locations that are close
                if (GameMain.GameSession.Campaign.Map.CurrentLocation.Connections.Contains(connection)) continue;
                HandleUpdate(connection.LevelData.MLC(), connection);
            }

            if (ShouldSpawnEventAtStart && !SpawnedEventAtStart)
            {
                TrySpawnEvent(GameMain.GameSession.Map, true);
                SpawnedEventAtStart = true;
            }
            else
            {
                TrySpawnEvent(GameMain.GameSession.Map, false);
            }
        }

        public override void OnPreRoundStart(LevelData levelData)
        {
            if (levelData == null) return;
            if (!Main.IsCampaign) return;

            if (!LevelHasEvent(levelData.MLC()) && !ForceSpawnMission)
            {
                Log.Debug($"Level has no {EventTag}");
                return;
            }

            // Never try to spawn a timed event on an outpost level
            if (levelData.Type == LevelData.LevelType.Outpost) return;

            if (TryGetMissionPrefab(levelData, out MissionPrefab prefab))
            {
                Log.Debug($"Adding {EventTag} mission");
                Mission inst = prefab.Instantiate(GameMain.GameSession.Map.SelectedConnection.Locations, Submarine.MainSub);
                AddExtraMission(inst); // we have to double add missions to make them work correctly
                _internalMissionStore.Add(inst);
                Log.Debug($"Added {EventTag} mission to extra missions!");
            }
            else
            {
                Log.Error($"Failed to find any {EventTag} missions!");
            }
        }

        protected virtual bool TryGetMissionPrefab(LevelData levelData, out MissionPrefab prefab)
        {
            return TryGetMissionByTag(EventTag, levelData, out prefab, ForcedMissionIdentifier);
        }

        protected void AddExtraMission(Mission mission)
        {
            List<Mission> _extraMissions = (List<Mission>)Instance.extraMissions.GetValue(GameMain.GameSession.GameMode);
            _extraMissions.Add(mission);
            Instance.extraMissions.SetValue(GameMain.GameSession.GameMode, _extraMissions);
        }

        public override void OnAddExtraMissions(CampaignMode __instance, LevelData levelData)
        {
            if (!_internalMissionStore.Any()) return;
            foreach (Mission mission in _internalMissionStore)
            {
                AddExtraMission(mission);
            }
            _internalMissionStore.Clear();
        }

        protected abstract void HandleUpdate(LevelData_MLCData data, LocationConnection connection);

        public void TrySpawnEvent(Map __instance, bool force = false)
        {
            if (Main.IsClient) return;
            // Check if we're at the max
            int activeEvents = __instance.Connections.Where(c => LevelHasEvent(c.LevelData.MLC())).Count();
            if (activeEvents > MaxActiveEvents)
            {
                if (force)
                {
                    Log.Debug($"Ignoring max {EventTag} cap due to force creation");
                }
                else
                {
                    Log.Verbose($"Skipped creating new {EventTag} due to being at the limit ({MaxActiveEvents})");
                    return;
                }
            }

            // If we're not, lets roll to see if we should make a new distress signal
            float chance = Rand.Value(Rand.RandSync.Unsynced);
            Log.InternalDebug($"{chance} <= {EventSpawnChance} ({chance <= EventSpawnChance}) for {EventTag}");
            if (chance >= EventSpawnChance && !force) return;

            // Lets get a random instance to use
            int seed = Rand.GetRNG(Rand.RandSync.Unsynced).Next();
            Random rand = new MTRandom(seed);

            // Find a location connection to spawn a distress beacon at
            int wantedEventSpawnDistance = Rand.Range(MinDistance, MaxDistance, Rand.RandSync.Unsynced);
            LocationConnection targetConnection = WalkConnection(__instance.CurrentLocation, rand, wantedEventSpawnDistance);
            if (targetConnection == null)
            {
                Log.Warn($"Failed to spawn new {EventTag} due to target connection being null.");
                return;
            }
            int duration = rand.Next(MinEventDuration, MaxEventDuration);

            if (!MapDirector.ConnectionIdLookup.ContainsKey(targetConnection)) return; // how does this happen?

            CreateEvent(targetConnection, duration);

#if SERVER
            if (GameMain.IsMultiplayer)
            {
                // inform clients of the new distress beacon
                IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.MAP_SEND_NEWDISTRESS);
                msg.WriteUInt32((uint)MapDirector.ConnectionIdLookup[targetConnection]);
                msg.WriteByte((byte)duration);
                NetUtil.SendAll(msg);
            }
            if (GameMain.GameSession?.GameMode is MultiPlayerCampaign campaign)
            {
                campaign.IncrementLastUpdateIdForFlag(MultiPlayerCampaign.NetFlags.MapAndMissions);
            }
#endif
        }


        protected abstract bool LevelHasEvent(LevelData_MLCData data);
        protected void CreateEvent(LocationConnection connection, int eventDuration)
        {
            HandleEventCreation(connection.LevelData.MLC(), eventDuration);
            AddNewsStory(NewEventText, connection);
        }
        protected abstract void HandleEventCreation(LevelData_MLCData data, int eventDuration);
    }
}

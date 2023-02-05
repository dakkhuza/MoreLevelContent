using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using static HarmonyLib.Code;

namespace MoreLevelContent.Missions
{
    // Shared
    partial class DistressSubmarineMission : DistressMission
    {
        private class MonsterSet
        {
            public readonly HashSet<(CharacterPrefab character, Point amountRange)> MonsterPrefabs = new HashSet<(CharacterPrefab character, Point amountRange)>();
            public float Commonness;

            public MonsterSet(XElement element) => Commonness = element.GetAttributeFloat("commonness", 100.0f);
        }

        private readonly XElement characterConfig;
        private readonly List<MonsterSet> monsterSets = new List<MonsterSet>();
        private readonly XElement submarineTypeConfig;
        private readonly LocalizedString sonarLabel;
        private readonly LocalizedString successCrew;
        private readonly LocalizedString successSub;
        private readonly LocalizedString revealedFailure;
        private readonly MissionNPCCollection missionNPCs;

        private readonly Dictionary<Character, int> rewardLookup = new();

        private Submarine lostSubmarine;
        private LevelData levelData;
        private bool swarmSpawned;
        private bool outsideOfSonarRange;
        private bool playerSubClose;
        private TrackingSonarMarker trackingSonarMarker;

        private bool SubSalvaged => lostSubmarine.AtEndExit || lostSubmarine.AtStartExit;
        private bool CrewResuced => missionNPCs.AnyHumanSurvived;

        public DistressSubmarineMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            // Setup submarine
            characterConfig = prefab.ConfigElement.GetChildElement("Characters");
            submarineTypeConfig = prefab.ConfigElement.GetChildElement("Submarine");

            // Setup text
            sonarLabel = TextManager.Get("missionname.distressmission");
            successCrew = TextManager.Get(prefab.ConfigElement.GetAttributeString("crewsurviveidentifier", "distress.submarinesuccess.crew"));
            successSub = TextManager.Get(prefab.ConfigElement.GetAttributeString("subsalvagedidentifer", "distress.submarinesuccess.sub"));
            revealedFailure = TextManager.Get(prefab.ConfigElement.GetAttributeString("revealedFailureidentifer", "distress.submarinefail"));

            // Setup monsters, copied from beacon station code
            swarmSpawned = false;
            foreach (var monsterElement in prefab.ConfigElement.GetChildElements("monster"))
            {
                if (!monsterSets.Any())
                {
                    monsterSets.Add(new MonsterSet(monsterElement));
                }
                LoadMonsters(monsterElement, monsterSets[0]);
            }
            foreach (var monsterSetElement in prefab.ConfigElement.GetChildElements("monsters"))
            {
                monsterSets.Add(new MonsterSet(monsterSetElement));
                foreach (var monsterElement in monsterSetElement.GetChildElements("monster"))
                {
                    LoadMonsters(monsterElement, monsterSets.Last());
                }
            }

            missionNPCs = new(this, characterConfig);

            // for campaign missions, set level at construction
            LevelData levelData = locations[0].Connections.Where(c => c.Locations.Contains(locations[1])).FirstOrDefault()?.LevelData ?? locations[0]?.LevelData;
            if (levelData != null)
            {
                SetLevel(levelData);
            }
        }

        // Allow override the default sonar label with a sonarlabel="" attribute
        public override LocalizedString SonarLabel => base.SonarLabel.IsNullOrEmpty() ? sonarLabel : base.SonarLabel;

        // Display a different failure message if the shuttle was located
        public override LocalizedString FailureMessage => state > 0 ? revealedFailure : base.FailureMessage;

        // Display a different success message if the crew is dead or alive at the end of the level
        public override LocalizedString SuccessMessage => state < 2 ? successCrew : successSub;


        public override IEnumerable<Vector2> SonarPositions
        {
            get
            {
                var empty = Enumerable.Empty<Vector2>();

                if (lostSubmarine == null) return empty;

                if (outsideOfSonarRange)
                {
                    return trackingSonarMarker != null ? trackingSonarMarker.GetEnumerable() : empty;
                }
                return empty;
            }
        }

        #region Reward
        
        public override int GetReward(Submarine sub) => Completed ? GetRewardCompleted() : GetRewardInLevel();

        int GetRewardCompleted()
        {
            int reward = 0;

            foreach (var character in missionNPCs.characters)
            {
                // Add payout for each living character
                if (MissionNPCCollection.Survived(character))
                {
                    reward += rewardLookup[character];
                    Log.Debug("Adjusted payout");
                }
            }

            if (SubSalvaged)
            {
                reward += Prefab.Reward;
                Log.Debug("Sub salvaged");
            }
            Log.Debug($"Get reward completed: {reward}");
            return reward;
        }

        int GetRewardInLevel()
        {
            int reward = Prefab.Reward;
            if (missionNPCs?.characters?.Count > 0)
            {
                foreach (var character in missionNPCs.characters)
                {
                    // Add payout for each living character
                    if (MissionNPCCollection.IsAlive(character))
                    {
                        reward += rewardLookup[character];
                        Log.Debug($"Added {rewardLookup[character]} to reward");
                    }
                    else Log.Debug("Character is dead");
                }
            }
            else Log.Debug("No Characters");

            Log.Debug($"Reward: {reward}");
            return reward;
        }
        #endregion

        private void LoadMonsters(XElement monsterElement, MonsterSet set)
        {
            Identifier speciesName = monsterElement.GetAttributeIdentifier("character", Identifier.Empty);
            int defaultCount = monsterElement.GetAttributeInt("count", -1);
            if (defaultCount < 0)
            {
                defaultCount = monsterElement.GetAttributeInt("amount", 1);
            }
            int min = Math.Min(monsterElement.GetAttributeInt("min", defaultCount), 255);
            int max = Math.Min(Math.Max(min, monsterElement.GetAttributeInt("max", defaultCount)), 255);
            var characterPrefab = CharacterPrefab.FindBySpeciesName(speciesName);
            if (characterPrefab != null)
            {
                _ = set.MonsterPrefabs.Add((characterPrefab, new Point(min, max)));
            }
            else
            {
                DebugConsole.ThrowError($"Error in distress submarine mission \"{Prefab.Identifier}\". Could not find a character prefab with the name \"{speciesName}\".");
            }
        }

        public override void SetLevel(LevelData level)
        {
            if (levelData != null)
            {
                //level already set
                return;
            }

            levelData = level;
            ContentPath subPath = submarineTypeConfig.GetAttributeContentPath("path", Prefab.ContentPackage);
            if (subPath.IsNullOrEmpty())
            {
                Log.Error($"No path used for submarine for the shuttle rescue mission \"{Prefab.Identifier}\"!");
                return;
            }

            SubmarineFile file = ContentPackageManager.EnabledPackages.All.SelectMany(p => p.GetFiles<SubmarineFile>()).Where(f => f.Path.Value == subPath).FirstOrDefault();
            if (file == null)
            {
                Log.Error($"Failed to find submarine at path {subPath}");
                return;
            }

            MissionGenerationDirector.RequestSubmarine(file, OnSubCreated);
        }


        void OnSubCreated(Submarine submarine)
        {
            submarine.SetPosition(submarine.FindSpawnPos(submarine.WorldPosition));
            lostSubmarine = submarine;
            lostSubmarine.Info.Type = SubmarineType.BeaconStation;
            submarine.TeamID = CharacterTeamType.FriendlyNPC;
            lostSubmarine.ShowSonarMarker = false;
            submarine.PhysicsBody.FarseerBody.BodyType = FarseerPhysics.BodyType.Dynamic;

            MissionUtils.PositionSubmarine(submarine, Level.PositionType.SidePath | Level.PositionType.MainPath);

            //make the shuttle resist at least it's spawn position + 100m
            submarine.RealWorldCrushDepth = Math.Max(Submarine.MainSub.RealWorldCrushDepth, Level.Loaded.GetRealWorldDepth(submarine.Position.Y) + 100);

            // tag all sub waypoints
            submarine.TagSubmarineWaypoints("distress_shuttle");

            // Init sonar tracking 
            trackingSonarMarker = new TrackingSonarMarker(30, submarine);
            TriggerEvents(0);
        }

        protected override void StartMissionSpecific(Level level)
        {
            if (!IsClient) StartServer();
        }

        void StartServer()
        {
            SinkSub();
            lostSubmarine.EnableMaintainPosition();

            Item sonarItem = Item.ItemList.Find(it => it.Submarine == lostSubmarine && it.GetComponent<Sonar>() != null);
            if (sonarItem == null)
            {
                DebugConsole.ThrowError($"No sonar found in the beacon station \"{lostSubmarine.Info.Name}\"!");
                return;
            }

            // Always allow the lost sub sonar to run so it attracts monsters :)
            Powered sonarPower = sonarItem.GetComponent<Powered>();
            sonarPower.MinVoltage = 0;

            var sonar = sonarItem.GetComponent<Sonar>();
            var steering = sonarItem.GetComponent<Steering>();
            sonar.CurrentMode = Sonar.Mode.Active;
            // Notify clients of the sonar's state
#if SERVER
                sonar.Item.CreateServerEvent(sonar);
#endif


            bool givenCharge = false;
            // Drain all of the batteries on the shuttle
            foreach (var item in lostSubmarine.GetItems(alsoFromConnectedSubs: false).Where(i => i.HasTag("battery") && !i.NonInteractable))
            {
                if (item.GetComponent<PowerContainer>() is PowerContainer powerContainer)
                {
                    // Allow fast recharging
                    powerContainer.MaxRechargeSpeed = powerContainer.Capacity;

                    // Drain batteries, give them a little bit of power
                    powerContainer.Charge = givenCharge ? 0 : 10;
                    givenCharge = true;
                }
            }

            // TODO: Drain any rods that are inside a reactor, if the shuttle has one

            // Init NPCS
            missionNPCs.CreateHumansInSubmarine(lostSubmarine, onCharacterCreated: (character, config) =>
            {
                int payout = config.GetAttributeInt("payout", 0);
                rewardLookup.Add(character, payout);
                character.Info.Title = TextManager.GetWithVariable("currencyformat", "[credits]", string.Format(CultureInfo.InvariantCulture, "{0:N0}", payout));

                ((HumanAIController)character.AIController).InitMentalStateManager();

                if (config.GetAttributeBool("isCaptain", false))
                {
                    // Set shuttle captain orders
                    var fightIntruders = OrderPrefab.Prefabs["fightintruders"].CreateInstance(OrderPrefab.OrderTargetType.Entity);
                    var repairBrokenDevices = OrderPrefab.Prefabs["repairsystems"].CreateInstance(OrderPrefab.OrderTargetType.Entity);
                    var fixLeaks = OrderPrefab.Prefabs["fixleaks"].CreateInstance(OrderPrefab.OrderTargetType.Entity);

                    character.SetOrder(fixLeaks, true, false);
                    character.SetOrder(fightIntruders, true, false);
                    character.SetOrder(repairBrokenDevices, true, false);

                    Log.InternalDebug("Updated captain orders");
                }
            });


            void SinkSub()
            {
                HashSet<Hull> ballastHulls = new HashSet<Hull>();
                foreach (Item item in Item.ItemList)
                {
                    if (item.Submarine != lostSubmarine) { continue; }
                    var pump = item.GetComponent<Pump>();
                    if (pump == null || item.CurrentHull == null) { continue; }
                    if (!item.HasTag("ballast") && !item.CurrentHull.RoomName.Contains("ballast", StringComparison.OrdinalIgnoreCase)) { continue; }
                    pump.FlowPercentage = 0.0f;
                    _ = ballastHulls.Add(item.CurrentHull);
                }

                foreach (Hull hull in Hull.HullList)
                {
                    if (hull.Submarine != lostSubmarine) { continue; }
                    if (!ballastHulls.Contains(hull)) { continue; }
                    hull.WaterVolume = hull.Volume;
                }
            }
        }

        // state 0 = init
        // state 1 = crew alive, escort to end
        // state 2 = crew dead, escort sub to end
        readonly float spawnDist = Sonar.DefaultSonarRange * 4;

        protected override void UpdateMissionSpecific(float deltaTime)
        {
            UpdateLastPing(deltaTime);
            if (IsClient) return;

            switch (state)
            {
                // init
                case 0:
                    float dist = Vector2.DistanceSquared(lostSubmarine.WorldPosition, Submarine.MainSub.WorldPosition);
                    if (dist > spawnDist * spawnDist) return;
                    if (!swarmSpawned) SpawnSwarm();
                    if (playerSubClose && swarmSpawned)
                    {
                        State = missionNPCs.AnyHumanAlive ? 1 : 2;
                    }
                    break;

                // crew alive
                case 1:
                    if (!missionNPCs.AnyHumanAlive)
                    {
                        State = 2;
                    }
                    break;

                // crew dead
                case 2:
                    break;
            }
        }

        readonly float sonarClose = Sonar.DefaultSonarRange / 0.8f;
        void UpdateLastPing(float deltaTime)
        {
            outsideOfSonarRange = Vector2.DistanceSquared(lostSubmarine.WorldPosition, Submarine.MainSub.WorldPosition) > Sonar.DefaultSonarRange * Sonar.DefaultSonarRange;
            playerSubClose = Vector2.DistanceSquared(lostSubmarine.WorldPosition, Submarine.MainSub.WorldPosition) < sonarClose * sonarClose;

            trackingSonarMarker.Update(deltaTime);
        }

        void SpawnSwarm()
        {
            swarmSpawned = true;
            // Find spawn position for the monsters

            if (monsterSets.Count == 0) return;
            Vector2 spawnPos = lostSubmarine.WorldPosition;
            spawnPos.Y += lostSubmarine.GetDockedBorders().Height * 1.5f;
            var monsterSet = ToolBox.SelectWeightedRandom(monsterSets, m => m.Commonness, Rand.RandSync.Unsynced);
            foreach ((CharacterPrefab monsterSpecies, Point monsterCountRange) in monsterSet.MonsterPrefabs)
            {
                int amount = Rand.Range(monsterCountRange.X, monsterCountRange.Y + 1);
                for (int i = 0; i < amount; i++)
                {
                    _ = CoroutineManager.Invoke(() =>
                    {
                        //round ended before the coroutine finished
                        if (GameMain.GameSession == null || Level.Loaded == null) { return; }
                        Entity.Spawner.AddCharacterToSpawnQueue(monsterSpecies.Identifier, spawnPos);
                    }, Rand.Range(0f, amount));
                }
            }
        }

        // Allow getting the sub out at either exits
        protected override bool DetermineCompleted() => SubSalvaged || CrewResuced;

        protected override void EndMissionSpecific(bool completed)
        {
            if (!IsClient) missionNPCs.End(completed);
            missionNPCs.Clear();

            base.EndMissionSpecific(completed);
        }
    }
}

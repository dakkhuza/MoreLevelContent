using Barotrauma;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Extensions;
using HarmonyLib;
using System;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared.Utils;
using System.Collections.Generic;
using Steamworks.Data;
using Barotrauma.Networking;

namespace MoreLevelContent.Missions
{
    // Shared
    partial class DistressGhostshipMission : DistressMission
    {
        private readonly LocalizedString defaultSonarLabel;
        private readonly XElement characterConfig;
        private readonly XElement submarineConfig;
        private readonly XElement decalConfig;
        private readonly XElement damageDevices;
        private readonly MissionNPCCollection missionNPCs;

        private readonly TravelTarget travelTarget;
        private readonly bool reactorActive;
        private readonly Level.PositionType spawnPosition;

        private Submarine ghostship;
        private LevelData levelData;
        private TrackingSonarMarker trackingSonarMarker;


        enum TravelTarget
        {
            Start,
            Maintain,
            End
        }

        public DistressGhostshipMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            // Config
            submarineConfig = prefab.ConfigElement.GetChildElement("submarines");
            characterConfig = prefab.ConfigElement.GetChildElement("characters");
            decalConfig = prefab.ConfigElement.GetChildElement("decals");
            damageDevices = prefab.ConfigElement.GetChildElement("damageDevices");

            // Top level attributes
            travelTarget = submarineConfig.GetAttributeEnum("TravelTarget", TravelTarget.Maintain);
            spawnPosition = submarineConfig.GetAttributeEnum("SpawnPosition", Level.PositionType.MainPath);
            reactorActive = submarineConfig.GetAttributeBool("ReactorActive", true);

            // General
            defaultSonarLabel = TextManager.Get("missionname.distressmission");
            missionNPCs = new(this, characterConfig);

            // for campaign missions, set level at construction
            LevelData levelData = locations[0].Connections.Where(c => c.Locations.Contains(locations[1])).FirstOrDefault()?.LevelData ?? locations[0]?.LevelData;
            if (levelData != null)
            {
                SetLevel(levelData);
            }
        }

        public override IEnumerable<(LocalizedString Label, Vector2 Position)> SonarLabels
        {
            get
            {
                if (ghostship == null) yield break;
                yield return trackingSonarMarker.CurrentPosition;
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
            List<(float, ContentPath)> submarines = new List<(float, ContentPath)>();
            foreach (var sub in submarineConfig.GetChildElements("sub"))
            {
                ContentPath path = sub.GetAttributeContentPath("path", Prefab.ContentPackage);
                int commenness = sub.GetAttributeInt("commonness", 0);
                submarines.Add((commenness, path));
            }

            ContentPath subPath = ToolBox.SelectWeightedRandom(submarines, s => s.Item1, Rand.RandSync.ServerAndClient).Item2;
            
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
            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));
            ghostship = submarine;
            ghostship.FlipX();
            submarine.ShowSonarMarker = false;
            submarine.TeamID = CharacterTeamType.None;
            ghostship.Info.Type = SubmarineType.BeaconStation;
            submarine.PhysicsBody.FarseerBody.BodyType = FarseerPhysics.BodyType.Dynamic;

            MissionUtils.PositionSubmarine(submarine, Level.PositionType.MainPath);

            // ensure the sub doesn't get crushed
            submarine.RealWorldCrushDepth = Math.Max(Submarine.MainSub.RealWorldCrushDepth, Level.Loaded.GetRealWorldDepth(submarine.Position.Y) + 1000);

            double minFlood = submarineConfig.GetAttributeDouble("minfloodpercentage", 0);
            double maxFlood = submarineConfig.GetAttributeDouble("maxfloodpercentage", 0);
            string[] floodHulls = submarineConfig.GetAttributeStringArray("floodtargets", new string[] { }, convertToLowerInvariant: true);
            if (floodHulls.Length > 0 && minFlood >= 0 && maxFlood > 0)
            {
                List<Hull> validHulls = ghostship.GetHulls(false).Where(h => floodHulls.Contains(h.RoomName.ToLowerInvariant())).ToList();
                foreach (var target in validHulls)
                {
                    target.WaterVolume = (float)(target.Volume * ((rand.NextDouble() * (maxFlood - minFlood)) + minFlood));
                    Log.Debug($"Flooded hull {target.RoomName}");
                }
            } else
            {
                Log.Debug("No hulls to flood");
            }

            // tag all sub waypoints
            submarine.TagSubmarineWaypoints("distress_ghostship");

            // Init tracking sonar marker
            trackingSonarMarker = new TrackingSonarMarker(30, submarine, Prefab.SonarLabel.IsNullOrEmpty() ? defaultSonarLabel : Prefab.SonarLabel);
        }

        private void SpawnDecals()
        {
            if (decalConfig == null) return;
            foreach (XElement item in decalConfig.GetChildElements("item"))
            {
                string prefab = item.GetAttributeString("prefab", "");
                string preferedHullName = item.GetAttributeString("preferedhull", "");
                int count = item.GetAttributeInt("count", 0);
                if (count == 0 || string.IsNullOrWhiteSpace(prefab)) continue;

                PlaceDecals(prefab, preferedHullName, count);
            }
        }

        private void PlaceDecals(string decalName, string preferedHull, int count)
        {
            try
            {
                bool hasPreferedHull = !string.IsNullOrWhiteSpace(preferedHull);
                Random rand = new MTRandom(ToolBox.StringToInt(level.Seed));
                List<Hull> filteredHulls = ghostship.GetHulls(false).Where(h => !h.RoomName.Contains("ballast") && !h.RoomName.Contains("airlock") && !h.IsWetRoom).ToList();
                var preferedHulls = filteredHulls.Where(h => h.RoomName.ToLowerInvariant() == preferedHull.ToLowerInvariant());

                for (int i = 0; i < count; i++)
                {
                    Hull hull = filteredHulls.ToList().GetRandom(rand);
                    if (hasPreferedHull && preferedHull.Any())
                    {
                        hull = preferedHulls.ToList().GetRandom(rand);
                        Log.Debug($"Set prefered hull, roomname {hull.RoomName}");
                    }

                    Vector2 pos = new Vector2(hull.WorldPosition.X + rand.Next(-hull.RectWidth / 2, hull.RectWidth / 2), hull.WorldPosition.Y + rand.Next(-hull.RectHeight / 2, hull.RectHeight / 2));
                    Decal decal = hull.AddDecal(decalName, pos, 1.0f, false);
                }
            } catch(Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        private void DamageDevices()
        {
            if (damageDevices == null) return;
            foreach (XElement device in damageDevices.GetChildElements("item"))
            {
                Identifier tag = device.GetAttributeIdentifier("tag", "");
                int condition = device.GetAttributeInt("condition", 0);
                int amount = device.GetAttributeInt("amount", 1);
                bool all = device.GetAttributeBool("all", false);
                if (tag.IsEmpty) continue;
                DamageDevice(tag, condition, amount, all);
            }
        }

        private void DamageDevice(Identifier tag, int condition, int amount, bool all)
        {
            Random rand = new MTRandom(ToolBox.StringToInt(level.Seed));
            var validItems = ghostship.GetItems(false).Where(i => i.IsPlayerTeamInteractable && i.HasTag(tag)).ToList();
            if (!validItems.Any()) return;

            if (all)
            {
                validItems.ForEach(i => Damage(i));
                return;
            }

            while(amount > 0 && validItems.Any())
            {
                amount--;
                Item target = validItems.GetRandom(rand);
                _ = validItems.Remove(target);
                Damage(target);
            }

            void Damage(Item item)
            {
                if (item.GetComponent<Repairable>() is Repairable repairable)
                {
                    item.Condition = condition;
                }
                Log.Debug("Damaged Device");
            }
        }

        protected override void StartMissionSpecific(Level level)
        {
            if (!IsClient)
            {
                StartServer();
                InitShip();
            }
        }

        void StartServer()
        {
            missionNPCs.CreateHumansInSubmarine(ghostship, onCharacterCreated: (character, config) =>
            {
                if (character.AIController is not HumanAIController humanAI) return;

                bool alive = config.GetAttributeBool("alive", true);
                if (!alive)
                {
                    character.Kill(CauseOfDeathType.Unknown, causeOfDeathAffliction: null, log: false);
                }
                else
                {
                    humanAI.InitMentalStateManager();
                }

                int minMoney = config.GetAttributeInt("minmoney", 0);
                int maxMoney = config.GetAttributeInt("maxmoney", 0);

                if (maxMoney > 0)
                {
                    int money = Rand.Range(minMoney, maxMoney, Rand.RandSync.Unsynced);
                    character.Wallet.Give(money);
                    Log.InternalDebug($"Gave {money} to {character.Name}");
                }

                foreach (var affliction in config.GetChildElements("affliction"))
                {
                    string identifier = affliction.GetAttributeString("identifier", null);
                    float strength = affliction.GetAttributeFloat("strength", 1);
                    bool targetRandomLimb = affliction.GetAttributeBool("randomLimb", false);
                    bool randomStrength = affliction.GetAttributeBool("randomStrength", false);
                    int count = affliction.GetAttributeInt("count", 1);
                
                    if (AfflictionHelper.TryGetAffliction(identifier, out AfflictionPrefab prefab))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            Limb targetLimb = character.AnimController.MainLimb;
                            if (targetRandomLimb) targetLimb = character.AnimController.Limbs.GetRandomUnsynced();
                            if (randomStrength) strength = Rand.Range(10f, 70f, Rand.RandSync.Unsynced);
                            character.CharacterHealth.ApplyAffliction(targetLimb, new Affliction(prefab, strength));
                        }
                    } else
                    {
                        Log.Error($"Unable to get affliction with identifier {identifier}");
                    }
                }
                character.CharacterHealth.ForceUpdateVisuals();
            });
            // Reputation stuff
            Hooks.Instance.OnStructureDamaged += OnStructureDamaged;
        }
        void InitShip()
        {
            ghostship.NeutralizeBallast();
            var ghostshipItems = ghostship.GetItems(alsoFromConnectedSubs: false);

            if (reactorActive && ghostshipItems.Find(i => i.HasTag("reactor") && !i.NonInteractable)?.GetComponent<Reactor>() is Reactor reactor)
            {
                Item reactorItem = reactor.Item;
                ItemContainer container = reactorItem.GetComponent<ItemContainer>();
                reactor.PowerUpImmediately();
                reactor.FuelConsumptionRate = 0;

                // ItemPrefab rod = ItemPrefab.Find(null, "fuelrod".ToIdentifier());


                // Make sure the reactor doesn't explode or irradiate the bots
                if (CompatabilityHelper.Instance.HazardousReactorsInstalled) CompatabilityHelper.SetupHazReactor(reactor);
                Repairable repairable = reactor.Item.GetComponent<Repairable>();
                if (repairable != null)
                {
                    repairable.DeteriorationSpeed = 0.0f;
                }
            }

            // make sure shit doesn't break by itself
            ghostshipItems.FindAll(i => i.HasTag("junctionbox") || i.HasTag("oxygengenerator")).ForEach(i =>
            {
                if (i.GetComponent<Repairable>() is Repairable repairable)
                {
                    repairable.DeteriorationSpeed = 0;
                }
            });

            Item sonarItem = Item.ItemList.Find(it => it.Submarine == ghostship && it.GetComponent<Sonar>() != null);
            if (sonarItem == null)
            {
                DebugConsole.ThrowError($"No sonar found in the beacon station \"{ghostship.Info.Name}\"!");
                return;
            }

            var steering = sonarItem.GetComponent<Steering>();
            steering.AutoPilot = true;
            switch (travelTarget)
            {
                case TravelTarget.Start:
                    steering.SetDestinationLevelStart();
                    break;
                case TravelTarget.Maintain:
                    steering.MaintainPos = true;
                    steering.PosToMaintain = ghostship.WorldPosition;
                    break;
                case TravelTarget.End:
                    steering.SetDestinationLevelEnd();
                    break;
            }
        }

        readonly float detectDist = Sonar.DefaultSonarRange;
        private bool finalSubSetup = false;
        protected override void UpdateMissionSpecific(float deltaTime)
        {
            if (!finalSubSetup && !IsClient)
            {
                SpawnDecals();
                DamageDevices();
                Log.InternalDebug("Preformed final sub setup");
                finalSubSetup = true;
            }
            trackingSonarMarker.Update(deltaTime);

            bool crewMemberInSub = CrewInSub();
            switch (State)
            {
                case 0:
                    float dist = Vector2.DistanceSquared(ghostship.WorldPosition, Submarine.MainSub.WorldPosition);
                    if (dist < detectDist * detectDist || crewMemberInSub)
                    {
                        State = 1;
                        Log.InternalDebug("State -> 1");
                    }
                    break;

                case 1:
                    if (crewMemberInSub)
                    {
                        State = 2;
                        Log.InternalDebug("State -> 2");
                    }
                    break;
                default:
                    break;
            }

            if (IsClient) return;

            bool CrewInSub()
            {
                foreach (var crewMember in GameSession.GetSessionCrewCharacters(CharacterType.Player))
                {
                    if (crewMember.Submarine == ghostship)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        float accumulatedDamage = 0;
        const float ACCUMULATED_DAMAGE_BREAKPOINT = 20;
        const float MAX_REP_LOSS = 10;
        bool displayedWarning = false;
        private void OnStructureDamaged(Structure structure, float damageAmount, Character character)
        {
            if (character == null || damageAmount <= 0.0f) { return; }
            if (!character.IsPlayer) { return; }
            if (structure?.Submarine == null || structure.Submarine != ghostship) { return; }

            // let them accidentally damage the hull a bit
            if (damageAmount <= 1.5f && accumulatedDamage < ACCUMULATED_DAMAGE_BREAKPOINT)
            {
                accumulatedDamage += damageAmount;
                return;
            }

            if (!displayedWarning)
            {
                displayedWarning = true;
                accumulatedDamage = 0;

#if SERVER
                GameMain.Server?.SendChatMessage(TextManager.GetServerMessage("distress.ghostship.damagenotification")?.Value, ChatMessageType.Default);
#endif
                return;
            }

            if (GameMain.GameSession?.Campaign?.Map?.CurrentLocation?.Reputation != null)
            {
                var reputationLoss = damageAmount * Reputation.ReputationLossPerWallDamage;
                reputationLoss = Math.Min(reputationLoss, 10); // clamp rep loss to a value 0-10
                GameMain.GameSession.Campaign.Map.CurrentLocation.Reputation.AddReputation(-reputationLoss);
            }
        }

        protected override bool DetermineCompleted() => State == 2;

        protected override void EndMissionSpecific(bool completed)
        {
            Hooks.Instance.OnStructureDamaged -= OnStructureDamaged;
            base.EndMissionSpecific(completed);
            missionNPCs.Clear();
        }
    }
}

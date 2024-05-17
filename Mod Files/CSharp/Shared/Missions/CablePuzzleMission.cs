using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MoreLevelContent.Missions
{
    // Shared
    internal partial class CablePuzzleMission : Mission
    {
        private readonly XElement _SubmarineConfig;
        private Submarine _Station;
        private LevelData _LevelData;
        const float INTERVAL = 15f;
        private float _Timer = INTERVAL;

        public CablePuzzleMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            _SubmarineConfig = prefab.ConfigElement.GetChildElement("Submarine");
            Log.Debug("Cable puzzle init'd");
        }

        public override IEnumerable<(LocalizedString Label, Vector2 Position)> SonarLabels
        {
            get
            {
                if (_Station == null) yield break;
                yield return (Prefab.SonarLabel, _Station.WorldPosition);
            }
        }

        public override void SetLevel(LevelData level)
        {
            if (_LevelData != null)
            {
                //level already set
                return;
            }
            _LevelData = level;
            ContentPath subPath = _SubmarineConfig.GetAttributeContentPath("path", Prefab.ContentPackage);

            if (subPath.IsNullOrEmpty())
            {
                Log.Error($"No path used for submarine for the shuttle rescue mission \"{Prefab.Identifier}\"!");
                return;
            }

            SubmarineFile file = ContentPackageManager.EnabledPackages.All.SelectMany(p => p.GetFiles<SubmarineFile>()).Where(f =>
            {
                Log.Debug(f.Path.Value);
                return f.Path.Value == subPath;
            }).FirstOrDefault();

            if (file == null)
            {
                Log.Error($"Failed to find submarine at path {subPath}");
                return;
            }

            MissionGenerationDirector.RequestSubmarine(new MissionGenerationDirector.SubmarineSpawnRequest()
            {
                File = file,
                Callback = OnSubCreated,
                SpawnPosition = Level.PositionType.Wreck,
                AllowStealing = true,
                PlacementType = Level.PlacementType.Top
            });
            Log.Debug("Added sub to request queue");
        }


        private MemoryComponent _WpInput;
        private MemoryComponent _WpTarget;
        private LightComponent _WpLight;

        private readonly MemoryComponent[] _MulComps = new MemoryComponent[4];
        private readonly MemoryComponent[] _DivComps = new MemoryComponent[4];
        private readonly MemoryComponent[] _AddComps = new MemoryComponent[4];
        private readonly MemoryComponent[] _SubComps = new MemoryComponent[4];
        private readonly int[] _Sequence = new int[4];

        void OnSubCreated(Submarine submarine)
        {
            _Station = submarine;

            submarine.PhysicsBody.FarseerBody.BodyType = FarseerPhysics.BodyType.Static;
            submarine.TeamID = CharacterTeamType.FriendlyNPC;
            Log.Debug("Set sub");
            if (IsClient) return;

            var items = submarine.GetItems(false);
            foreach (var item in items)
            {
                if (item.HasTag("wp_input")) _WpInput = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_target")) _WpTarget = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_light")) _WpLight = item.GetComponent<LightComponent>();

                if (item.HasTag("wp_multi0")) _MulComps[0] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_multi1")) _MulComps[1] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_multi2")) _MulComps[2] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_multi3")) _MulComps[3] = item.GetComponent<MemoryComponent>();

                if (item.HasTag("wp_div0")) _DivComps[0] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_div1")) _DivComps[1] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_div2")) _DivComps[2] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_div3")) _DivComps[3] = item.GetComponent<MemoryComponent>();

                if (item.HasTag("wp_add0")) _AddComps[0] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_add1")) _AddComps[1] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_add2")) _AddComps[2] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_add3")) _AddComps[3] = item.GetComponent<MemoryComponent>();

                if (item.HasTag("wp_sub0")) _SubComps[0] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_sub1")) _SubComps[1] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_sub2")) _SubComps[2] = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_sub3")) _SubComps[3] = item.GetComponent<MemoryComponent>();
            }
            Log.Debug("Items good");

            var rand = MLCUtils.GetRandomFromString(_LevelData.Seed);

            foreach (var item in _MulComps)
            {
                if (item == null)
                {
                    Log.Error("Mult null");
                    continue;
                }
                item.Value = rand.Next(5, 25).ToString();
                Log.Debug(item.Value);
            }

            foreach (var item in _DivComps)
            {
                if (item == null)
                {
                    Log.Error("Div null");
                    continue;
                }
                item.Value = rand.Next(5, 25).ToString();
                Log.Debug(item.Value);
            }

            foreach (var item in _AddComps)
            {
                if (item == null)
                {
                    Log.Error("Add null");
                    continue;
                }
                item.Value = rand.Next(15, 55).ToString();
                Log.Debug(item.Value);
            }

            foreach (var item in _SubComps)
            {
                if (item == null)
                {
                    Log.Error("Sub null");
                    continue;
                }
                item.Value = rand.Next(15, 55).ToString();
                Log.Debug(item.Value);
            }

            // Build random sequence
            _Sequence[0] = int.Parse(_MulComps.GetRandom(rand).Value);
            _Sequence[1] = int.Parse(_DivComps.GetRandom(rand).Value);
            _Sequence[2] = int.Parse(_AddComps.GetRandom(rand).Value);
            _Sequence[3] = int.Parse(_SubComps.GetRandom(rand).Value);
            Log.Debug("Sub created");
        }

        protected override void UpdateMissionSpecific(float deltaTime)
        {
            if (IsClient) return;
            if (_Station == null) return;
            if (_Timer > 0)
            {
                _Timer -= deltaTime;
                return;
            }

            var input = Rand.Range(5, 100, Rand.RandSync.Unsynced);
            _WpInput.Value = input.ToString();

            try
            {
                input *= _Sequence[0];
                input /= _Sequence[1];
                input += _Sequence[2];
                input -= _Sequence[3];
            } catch { }

            _WpTarget.Value = input.ToString();
            _Timer = INTERVAL;
        }

        protected override bool DetermineCompleted() => _WpLight.IsOn;
    }
}

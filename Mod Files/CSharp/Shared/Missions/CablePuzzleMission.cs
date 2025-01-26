using Barotrauma;
using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Xml.Linq;

namespace MoreLevelContent.Missions
{
    // Shared
    internal partial class CablePuzzleMission : Mission
    {
        /// <summary>
        /// This sucks ass but we should /NEVER/ have more than one relay station in one level
        /// </summary>
        public static SubmarineFile SubmarineFile { get; set; }

        const float INTERVAL = 2.5f;
        const int RANGE_MIN = 50;
        const int RANGE_MAX = 200;
        const int REQUIRED_CYCLES = 2;
        const float REQUIRED_CYCLE_TIME = 1;
        const double CYCLE_GRACE_PERIOD = Timing.Step * 2;

        private readonly LocalizedString defaultSonarLabel;
        private readonly string terminalTag; 
        private readonly XElement _SubmarineConfig;
        private Submarine _Station;
        private LevelData _LevelData;
        private float _Timer = INTERVAL;

        public CablePuzzleMission(MissionPrefab prefab, Location[] locations, Submarine sub) : base(prefab, locations, sub)
        {
            _SubmarineConfig = prefab.ConfigElement.GetChildElement("Submarine");
            defaultSonarLabel = TextManager.Get("relaysonarlabel");
            terminalTag = _SubmarineConfig.GetAttributeString("welcomemsg", "relayrepair.terminal");
            SubmarineFile = null;
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
                if (_Station == null) yield break;
                yield return (Prefab.SonarLabel.IsNullOrEmpty() ? defaultSonarLabel : Prefab.SonarLabel, _Station.WorldPosition);
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
            SetSub(_SubmarineConfig, Prefab);
        }

        public static void SetSub(XElement config, MissionPrefab prefab)
        {
            ContentPath subPath = config.GetAttributeContentPath("path", prefab.ContentPackage);

            if (subPath.IsNullOrEmpty())
            {
                Log.Error($"No path used for submarine for the relay station mission \"{prefab.Identifier}\"!");
                return;
            }

            SubmarineFile file = ContentPackageManager.EnabledPackages.All.SelectMany(p => p.GetFiles<SubmarineFile>()).Where(f => f.Path.Value == subPath).FirstOrDefault();
            if (file == null)
            {
                Log.Error($"Failed to find submarine at path {subPath}");
                return;
            }
            SubmarineFile = file;
            Log.Debug("Set relay station sub file");
        }


        private MemoryComponent _WpInput;
        private MemoryComponent _WpTarget;
        private LightComponent _WpLight;
        private Terminal _WpHint;

        private readonly List<SignalOperation> _Operations = new();
        private readonly SignalOperation[] _Sequence = new SignalOperation[4];
        private enum OperationType
        {
            Add,
            Sub,
            Mul,
            Div
        }

        private struct SignalOperation
        {
            public SignalOperation(OperationType type, int value)
            {
                _Value = value;
                _Operation = type;
            }
            private readonly OperationType _Operation;
            private readonly int _Value;

            public override string ToString() => $"{_Operation} {_Value}";

            public int Run(int input)
            {
                switch (_Operation)
                {
                    case OperationType.Add:
                        return input += _Value;
                    case OperationType.Sub:
                        return input -= _Value;
                    case OperationType.Mul:
                        return input *= _Value;
                    case OperationType.Div:
                        return input /= _Value;
                }
                throw new NotImplementedException();
            }
        }


        protected override void StartMissionSpecific(Level level)
        {
            _Station = level.MLC().RelayStation;

            if (_Station == null)
            {
                Log.Error("Failed to spawn relay station!!");
                return;
            }

            if (IsClient) return;
            var items = _Station.GetItems(false);
            var rand = MLCUtils.GetRandomFromString(_LevelData.Seed);
            var memoryComps = new List<MemoryComponent>();
            foreach (var item in items)
            {
                if (item.HasTag("wp_input")) _WpInput = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_target")) _WpTarget = item.GetComponent<MemoryComponent>();
                if (item.HasTag("wp_light")) _WpLight = item.GetComponent<LightComponent>();
                if (item.HasTag("wp_hint")) _WpHint = item.GetComponent<Terminal>();

                if (item.HasTag("wp_add")) AddOperation(OperationType.Add);
                if (item.HasTag("wp_sub")) AddOperation(OperationType.Sub);
                if (item.HasTag("wp_mul")) AddOperation(OperationType.Mul);

                void AddOperation(OperationType type, int low = 5, int high = 25)
                {
                    var comp = item.GetComponent<MemoryComponent>();
                    if (comp == null)
                    {
                        Log.Error("Failed to find a memory component on tagged item");
                        return;
                    }
                    int val = rand.Next(low, high);
                    comp.Value = val.ToString();
                    memoryComps.Add(comp);

                    _Operations.Add(new SignalOperation(type, val));
                }
            }

            if (_Operations.Count == 0)
            {
                Log.Error("Failed to collect any operations!");
                return;
            }


            // Build random sequence
            _Operations.Shuffle(rand);

            _Sequence[0] = _Operations[0];
            _Sequence[1] = _Operations[1];
            _Sequence[2] = _Operations[2];
            _Sequence[3] = _Operations[3];

            int[] steps0 = GetStepsForInput(Rand.Range(RANGE_MIN, RANGE_MAX, Rand.RandSync.Unsynced));
            int[] steps1 = GetStepsForInput(Rand.Range(RANGE_MIN, RANGE_MAX, Rand.RandSync.Unsynced));
            int[] steps2 = GetStepsForInput(Rand.Range(RANGE_MIN, RANGE_MAX, Rand.RandSync.Unsynced));

            _WpHint.ShowMessage = TextManager.GetWithVariables("relayrepair.terminal",
                ("[version]", $"${Main.Version}"),
                ("[station]", $"{rand.Next(100, 999)}"),

                ("[input0]", $"{steps0[0].ToString().PadLeft(3, '0')}"),
                ("[input1]", $"{steps1[0].ToString().PadLeft(3, '0')}"),
                ("[input2]", $"{steps2[0].ToString().PadLeft(3, '0')}"),

                ("[step10]", $"{steps0[1].ToString().PadLeft(3, '0')}"),
                ("[step20]", $"{steps0[2].ToString().PadLeft(3, '0')}"),
                ("[step30]", $"{steps0[3].ToString().PadLeft(3, '0')}"),

                ("[step11]", $"{steps1[1].ToString().PadLeft(3, '0')}"),
                ("[step21]", $"{steps1[2].ToString().PadLeft(3, '0')}"),
                ("[step31]", $"{steps1[3].ToString().PadLeft(3, '0')}"),

                ("[step12]", $"{steps2[1].ToString().PadLeft(3, '0')}"),
                ("[step22]", $"{steps2[2].ToString().PadLeft(3, '0')}"),
                ("[step32]", $"{steps2[3].ToString().PadLeft(3, '0')}"),

                ("[output0]", $"{steps0[4]}"),
                ("[output1]", $"{steps1[4]}"),
                ("[output2]", $"{steps2[4]}")

                ).Value;

            Log.Debug("Sub created");

#if SERVER
            // Have the server send these changes to the client
            _WpHint.SyncHistory();
            foreach (var comp in memoryComps)
            {
                comp.Item.CreateServerEvent(comp);
            }
#endif
        }



        private int GetStepForInput(int input, int stepCount)
        {
            if (stepCount > _Sequence.Length)
            {
                Log.Error($"Requested sequence step ({stepCount}) is bigger then the sequence length ({_Sequence.Length})");
                return 0;
            }

            for (int i = 0; i < stepCount; i++)
            {
                var operation = _Sequence[i];
                input = operation.Run(input);
            }
            return input;
        }
        private int[] GetStepsForInput(int input)
        {
            int[] output = new int[_Sequence.Length + 1];
            output[0] = input;
            for (int i = 0; i < _Sequence.Length; i++)
            {
                input = _Sequence[i].Run(input);
                output[i + 1] = input;
            }
            return output;
        }

        double successTimer = 0;
        protected override void UpdateMissionSpecific(float deltaTime)
        {
            if (IsClient) return;
            if (_Station == null) return;

            if (State == 0)
            {
                if (CrewInSub())
                {
                    State = 1;
                }
            }

            // Crew has entered the relay and is fixing it
            if (State >= 1)
            {
                // We have the correct value
                if (_WpLight.IsOn)
                {
                    successTimer = 2f;
                } else if (successTimer > 0)
                {
                    successTimer -= deltaTime;
                }

                State = successTimer > 0 ? 2 : 1;
            }

            // Return if timer is counting
            if (_Timer > 0)
            {
                _Timer -= deltaTime;
                return;
            }

            var input = Rand.Range(RANGE_MIN, RANGE_MAX, Rand.RandSync.Unsynced);
            _WpInput.Value = input.ToString();
            _WpTarget.Value = GetStepForInput(input, 4).ToString();

#if SERVER
            // Have the server send the info to the client
            _WpInput.Item.CreateServerEvent(_WpInput);
            _WpTarget.Item.CreateServerEvent(_WpTarget);
#endif

            _Timer = INTERVAL;


            bool CrewInSub()
            {
                foreach (var crewMember in GameSession.GetSessionCrewCharacters(CharacterType.Player))
                {
                    if (crewMember.Submarine == _Station)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        protected override void EndMissionSpecific(bool completed)
        {
            if (completed && level.LevelData != null)
            {
                level.LevelData.MLC().RelayStationStatus = RelayStationStatus.Active;
            }
        }

        protected override bool DetermineCompleted() => State == 2;
    }
}

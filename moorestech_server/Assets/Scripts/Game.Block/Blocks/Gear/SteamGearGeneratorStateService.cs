using System;
using Core.Master;
using Core.Update;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    // SteamGearGeneratorの状態と出力を統括するサービス
    // Service that governs SteamGearGenerator state transitions and output levels
    public class SteamGearGeneratorStateService
    {
        // 出力変化を検知する際の最小閾値
        // Minimum delta used to detect meaningful output changes
        private const float RateChangeThreshold = 0.0001f;

        private enum GeneratorState
        {
            Idle,
            Accelerating,
            Running,
            Decelerating
        }

        // 依存するパラメータとコンポーネント、および内部状態保持用のフィールド群
        // Dependent parameters, collaborating components, and internal state holders
        private readonly SteamGearGeneratorBlockParam _param;
        private readonly SteamGearGeneratorFuelService _fuelService;
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;

        private GeneratorState _currentState;
        private float _stateElapsedTime;
        private float _steamConsumptionRate;
        private float _rateAtDecelerationStart;

        // 外部へ公開する読み取りプロパティ
        // Read-only accessors exposed to the outside world
        public float SteamConsumptionRate => _steamConsumptionRate;
        // 加速または稼働状態にあり、出力が立ち上がっているかの指標
        // Indicates whether the generator is accelerating or running with active output
        public bool IsReady => _currentState == GeneratorState.Accelerating || _currentState == GeneratorState.Running;
        public float RateAtDecelerationStart => _rateAtDecelerationStart;
        public string CurrentStateName => _currentState.ToString();
        public RPM CurrentGeneratedRpm => new RPM(_param.GenerateMaxRpm * _steamConsumptionRate);
        public Torque CurrentGeneratedTorque => new Torque(_param.GenerateMaxTorque * _steamConsumptionRate);

        // コンストラクタでサービスの依存を受け取り、初期状態を整える
        // Accept dependencies via constructor and initialise the state machine
        public SteamGearGeneratorStateService(
            SteamGearGeneratorBlockParam param,
            SteamGearGeneratorFuelService fuelService,
            SteamGearGeneratorFluidComponent fluidComponent)
        {
            _param = param;
            _fuelService = fuelService;
            _fluidComponent = fluidComponent;
            _currentState = GeneratorState.Idle;
            _stateElapsedTime = 0f;
            _steamConsumptionRate = 0f;
            _rateAtDecelerationStart = 0f;
        }

        // 状態を更新し、出力が変化した場合 true を返す
        // Advance the state machine and return true when the output changes
        public bool TryUpdate(out RPM generateRpm, out Torque generateTorque)
        {
            var previousRate = _steamConsumptionRate;
            var previousState = _currentState;

            ProcessStateMachine();
            UpdateConsumptionRate();

            generateRpm = CurrentGeneratedRpm;
            generateTorque = CurrentGeneratedTorque;

            var stateChanged = previousState != _currentState;
            var rateChanged = Mathf.Abs(previousRate - _steamConsumptionRate) > RateChangeThreshold;
            return stateChanged || rateChanged;
        }

        // 現在の内部状態をセーブ用スナップショットに変換する
        // Convert the internal state into a snapshot for save serialization
        public StateSnapshot CreateSnapshot()
        {
            return new StateSnapshot
            {
                State = _currentState.ToString(),
                StateElapsedTime = _stateElapsedTime,
                SteamConsumptionRate = _steamConsumptionRate,
                RateAtDecelerationStart = _rateAtDecelerationStart
            };
        }

        // セーブデータから状態機械の内部値を復元する
        // Restore internal values of the state machine from a save snapshot
        public void Restore(StateSnapshot snapshot)
        {
            if (!Enum.TryParse(snapshot.State, out GeneratorState parsedState))
            {
                parsedState = GeneratorState.Idle;
            }

            _currentState = parsedState;
            _stateElapsedTime = snapshot.StateElapsedTime;
            _steamConsumptionRate = snapshot.SteamConsumptionRate;
            _rateAtDecelerationStart = snapshot.RateAtDecelerationStart;
        }

        // 燃料状況とパイプ状態を考慮して状態遷移を進める
        // Advance state transitions based on available fuel and pipe connectivity
        private void ProcessStateMachine()
        {
            _fuelService.Update();

            var allowFluidFuel = !_fluidComponent.IsPipeDisconnected;
            var hasFuel = _fuelService.HasAvailableFuel(allowFluidFuel);
            var shouldForceDeceleration = _fuelService.IsUsingFluidFuel && !allowFluidFuel;
            _stateElapsedTime += (float)GameUpdater.UpdateSecondTime;

            switch (_currentState)
            {
                case GeneratorState.Idle:
                    if (hasFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Accelerating);
                    }
                    break;

                case GeneratorState.Accelerating:
                    if (shouldForceDeceleration)
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    else if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    else if (_stateElapsedTime >= _param.TimeToMax)
                    {
                        TransitionToState(GeneratorState.Running);
                    }
                    break;

                case GeneratorState.Running:
                    if (shouldForceDeceleration)
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    else if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    break;

                case GeneratorState.Decelerating:
                    if (_stateElapsedTime >= _param.TimeToMax)
                    {
                        TransitionToState(GeneratorState.Idle);
                    }
                    else if (hasFuel && allowFluidFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Accelerating);
                    }
                    break;
            }
        }

        // 現在の状態に応じた出力割合を更新する
        // Refresh the output ratio depending on the current machine state
        private void UpdateConsumptionRate()
        {
            switch (_currentState)
            {
                case GeneratorState.Idle:
                    _steamConsumptionRate = 0f;
                    break;
                case GeneratorState.Accelerating:
                    var accelerationProgress = Mathf.Clamp01(_stateElapsedTime / _param.TimeToMax);
                    _steamConsumptionRate = ApplyEasing(accelerationProgress, _param.TimeToMaxEasing);
                    break;
                case GeneratorState.Running:
                    _steamConsumptionRate = 1f;
                    break;
                case GeneratorState.Decelerating:
                    var decelerationProgress = Mathf.Clamp01(_stateElapsedTime / _param.TimeToMax);
                    var eased = ApplyEasing(decelerationProgress, _param.TimeToMaxEasing);
                    _steamConsumptionRate = _rateAtDecelerationStart * (1f - eased);
                    break;
            }
        }

        // 状態遷移時の共通処理をまとめたヘルパー
        // Helper that centralises common work required during state transitions
        private void TransitionToState(GeneratorState newState)
        {
            if (_currentState == newState) return;

            if (newState == GeneratorState.Decelerating)
            {
                _rateAtDecelerationStart = _steamConsumptionRate;
            }

            _currentState = newState;
            _stateElapsedTime = 0f;
        }

        // 指定されたイージング種別に基づき0〜1の補間値を算出する
        // Calculate eased interpolation value between 0 and 1 based on easing type
        private float ApplyEasing(float t, string easingType)
        {
            switch (easingType)
            {
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.Linear:
                    return t;
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInSine:
                    return 1 - Mathf.Cos((t * Mathf.PI) / 2f);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutSine:
                    return Mathf.Sin((t * Mathf.PI) / 2f);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCubic:
                    return t * t * t;
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCubic:
                    return 1 - Mathf.Pow(1 - t, 3);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInQuint:
                    return t * t * t * t * t;
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutQuint:
                    return 1 - Mathf.Pow(1 - t, 5);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCirc:
                    return 1 - Mathf.Sqrt(1 - t * t);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCirc:
                    return Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));
                default:
                    return t;
            }
        }

        // セーブ・ロードで用いる状態スナップショット
        // State snapshot structure used during save and load operations
        public struct StateSnapshot
        {
            public string State { get; set; }
            public float StateElapsedTime { get; set; }
            public float SteamConsumptionRate { get; set; }
            public float RateAtDecelerationStart { get; set; }
        }
    }
}

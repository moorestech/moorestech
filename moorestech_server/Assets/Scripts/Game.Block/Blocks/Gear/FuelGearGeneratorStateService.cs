using System;
using Core.Update;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public enum FuelGearGeneratorState
    {
        Idle,
        Accelerating,
        Running,
        Decelerating
    }
    
    // FuelGearGeneratorの状態と出力を統括するサービス
    // Service that governs FuelGearGenerator state transitions and output levels
    public class FuelGearGeneratorStateService
    {
        // 出力変化を検知する際の最小閾値
        // Minimum delta used to detect meaningful output changes
        private const float RateChangeThreshold = 0.0001f;
        

        // 依存するパラメータとコンポーネント、および内部状態保持用のフィールド群
        // Dependent parameters, collaborating components, and internal state holders
        public FuelGearGeneratorState CurrentState { get; private set; }
        public float SteamConsumptionRate { get; private set; }
        public uint StateElapsedTicks { get; private set; }
        public float RateAtDecelerationStart { get; private set; }

        private readonly FuelGearGeneratorBlockParam _param;
        private readonly FuelGearGeneratorFuelService _fuelService;
        private readonly FuelGearGeneratorFluidComponent _fluidComponent;
        private readonly uint _timeToMaxTicks;

        // 外部へ公開する読み取りプロパティ
        // Read-only accessors exposed to the outside world
        public RPM CurrentGeneratedRpm => new(_param.GenerateMaxRpm * SteamConsumptionRate);
        public Torque CurrentGeneratedTorque => new(_param.GenerateMaxTorque * SteamConsumptionRate);

        // コンストラクタでサービスの依存を受け取り、初期状態を整える
        // Accept dependencies via constructor and initialise the state machine
        public FuelGearGeneratorStateService(
            FuelGearGeneratorBlockParam param,
            FuelGearGeneratorFuelService fuelService,
            FuelGearGeneratorFluidComponent fluidComponent)
        {
            _param = param;
            _fuelService = fuelService;
            _fluidComponent = fluidComponent;
            _timeToMaxTicks = GameUpdater.SecondsToTicks(_param.TimeToMax);
            CurrentState = FuelGearGeneratorState.Idle;
            StateElapsedTicks = 0;
            SteamConsumptionRate = 0f;
            RateAtDecelerationStart = 0f;
        }

        // 状態を更新し、出力が変化した場合 true を返す
        // Advance the state machine and return true when the output changes
        public bool TryUpdate(float operatingRate, out RPM generateRpm, out Torque generateTorque)
        {
            var previousRate = SteamConsumptionRate;
            var previousState = CurrentState;

            ProcessStateMachine(operatingRate);
            UpdateConsumptionRate();

            generateRpm = CurrentGeneratedRpm;
            generateTorque = CurrentGeneratedTorque;

            var stateChanged = previousState != CurrentState;
            var rateChanged = Mathf.Abs(previousRate - SteamConsumptionRate) > RateChangeThreshold;
            return stateChanged || rateChanged;
        }

        // 燃料状況とパイプ状態を考慮して状態遷移を進める
        // Advance state transitions based on available fuel and pipe connectivity
        private void ProcessStateMachine(float operatingRate)
        {
            _fuelService.Update(operatingRate);

            var allowFluidFuel = !_fluidComponent.IsPipeDisconnected;
            var hasFuel = _fuelService.HasAvailableFuel(allowFluidFuel);
            StateElapsedTicks += GameUpdater.CurrentTickCount;

            switch (CurrentState)
            {
                case FuelGearGeneratorState.Idle:
                    if (hasFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(FuelGearGeneratorState.Accelerating);
                    }
                    break;

                case FuelGearGeneratorState.Accelerating:
                    if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(FuelGearGeneratorState.Decelerating);
                    }
                    else if (StateElapsedTicks >= _timeToMaxTicks)
                    {
                        TransitionToState(FuelGearGeneratorState.Running);
                    }
                    break;

                case FuelGearGeneratorState.Running:
                    if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(FuelGearGeneratorState.Decelerating);
                    }
                    break;

                case FuelGearGeneratorState.Decelerating:
                    if (StateElapsedTicks >= _timeToMaxTicks)
                    {
                        TransitionToState(FuelGearGeneratorState.Idle);
                    }
                    else if (hasFuel && allowFluidFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(FuelGearGeneratorState.Accelerating);
                    }
                    break;
            }

            #region Internal

            // 状態遷移時の共通処理をまとめたヘルパー
            // Helper that centralises common work required during state transitions
            void TransitionToState(FuelGearGeneratorState newState)
            {
                if (CurrentState == newState) return;

                if (newState == FuelGearGeneratorState.Decelerating)
                {
                    RateAtDecelerationStart = SteamConsumptionRate;
                }

                CurrentState = newState;
                StateElapsedTicks = 0;
            }

            #endregion
        }

        // 現在の状態に応じた出力割合を更新する
        // Refresh the output ratio depending on the current machine state
        private void UpdateConsumptionRate()
        {
            // tick数を比率に変換（0除算回避）
            // Convert ticks to ratio (avoid division by zero)
            var progressRatio = _timeToMaxTicks > 0 ? (float)StateElapsedTicks / _timeToMaxTicks : 1f;
            progressRatio = Mathf.Clamp01(progressRatio);

            switch (CurrentState)
            {
                case FuelGearGeneratorState.Idle:
                    SteamConsumptionRate = 0f;
                    break;
                case FuelGearGeneratorState.Accelerating:
                    SteamConsumptionRate = ApplyEasing(progressRatio, _param.TimeToMaxEasing);
                    break;
                case FuelGearGeneratorState.Running:
                    SteamConsumptionRate = 1f;
                    break;
                case FuelGearGeneratorState.Decelerating:
                    var eased = ApplyEasing(progressRatio, _param.TimeToMaxEasing);
                    SteamConsumptionRate = RateAtDecelerationStart * (1f - eased);
                    break;
            }
            
            #region Internal
            
            float ApplyEasing(float t, string easingType)
            {
                switch (easingType)
                {
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.Linear:
                        return t;
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInSine:
                        return 1 - Mathf.Cos((t * Mathf.PI) / 2f);
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutSine:
                        return Mathf.Sin((t * Mathf.PI) / 2f);
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCubic:
                        return t * t * t;
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCubic:
                        return 1 - Mathf.Pow(1 - t, 3);
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInQuint:
                        return t * t * t * t * t;
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutQuint:
                        return 1 - Mathf.Pow(1 - t, 5);
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCirc:
                        return 1 - Mathf.Sqrt(1 - t * t);
                    case FuelGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCirc:
                        return Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));
                    default:
                        return t;
                }
            }
            
            #endregion
        }
        
        
        
        // セーブデータから状態機械の内部値を復元する
        // Restore internal values of the state machine from a save snapshot
        public void Restore(FuelGearGeneratorSaveData saveData)
        {
            CurrentState = Enum.TryParse(saveData.CurrentState, out FuelGearGeneratorState parsedState) ? parsedState : FuelGearGeneratorState.Idle;
            StateElapsedTicks = saveData.StateElapsedTicks;
            SteamConsumptionRate = saveData.SteamConsumptionRate;
            RateAtDecelerationStart = saveData.RateAtDecelerationStart;
        }
    }
}

using System;
using Core.Update;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public enum SteamGearGeneratorState
    {
        Idle,
        Accelerating,
        Running,
        Decelerating
    }
    
    // SteamGearGeneratorの状態と出力を統括するサービス
    // Service that governs SteamGearGenerator state transitions and output levels
    public class SteamGearGeneratorStateService
    {
        // 出力変化を検知する際の最小閾値
        // Minimum delta used to detect meaningful output changes
        private const float RateChangeThreshold = 0.0001f;
        

        // 依存するパラメータとコンポーネント、および内部状態保持用のフィールド群
        // Dependent parameters, collaborating components, and internal state holders
        public SteamGearGeneratorState CurrentState { get; private set; }
        public float SteamConsumptionRate { get; private set; }
        public float StateElapsedTime { get; private set; }
        public float RateAtDecelerationStart { get; private set; }
        
        private readonly SteamGearGeneratorBlockParam _param;
        private readonly SteamGearGeneratorFuelService _fuelService;
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;

        // 外部へ公開する読み取りプロパティ
        // Read-only accessors exposed to the outside world
        public RPM CurrentGeneratedRpm => new(_param.GenerateMaxRpm * SteamConsumptionRate);
        public Torque CurrentGeneratedTorque => new(_param.GenerateMaxTorque * SteamConsumptionRate);

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
            CurrentState = SteamGearGeneratorState.Idle;
            StateElapsedTime = 0f;
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
            StateElapsedTime += (float)GameUpdater.UpdateSecondTime;

            switch (CurrentState)
            {
                case SteamGearGeneratorState.Idle:
                    if (hasFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(SteamGearGeneratorState.Accelerating);
                    }
                    break;

                case SteamGearGeneratorState.Accelerating:
                    if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(SteamGearGeneratorState.Decelerating);
                    }
                    else if (StateElapsedTime >= _param.TimeToMax)
                    {
                        TransitionToState(SteamGearGeneratorState.Running);
                    }
                    break;

                case SteamGearGeneratorState.Running:
                    if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(SteamGearGeneratorState.Decelerating);
                    }
                    break;

                case SteamGearGeneratorState.Decelerating:
                    if (StateElapsedTime >= _param.TimeToMax)
                    {
                        TransitionToState(SteamGearGeneratorState.Idle);
                    }
                    else if (hasFuel && allowFluidFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(SteamGearGeneratorState.Accelerating);
                    }
                    break;
            }
            
            #region Internal
            
            // 状態遷移時の共通処理をまとめたヘルパー
            // Helper that centralises common work required during state transitions
            void TransitionToState(SteamGearGeneratorState newState)
            {
                if (CurrentState == newState) return;
                
                if (newState == SteamGearGeneratorState.Decelerating)
                {
                    RateAtDecelerationStart = SteamConsumptionRate;
                }
                
                CurrentState = newState;
                StateElapsedTime = 0f;
            }
            
            #endregion
        }

        // 現在の状態に応じた出力割合を更新する
        // Refresh the output ratio depending on the current machine state
        private void UpdateConsumptionRate()
        {
            switch (CurrentState)
            {
                case SteamGearGeneratorState.Idle:
                    SteamConsumptionRate = 0f;
                    break;
                case SteamGearGeneratorState.Accelerating:
                    var accelerationProgress = Mathf.Clamp01(StateElapsedTime / _param.TimeToMax);
                    SteamConsumptionRate = ApplyEasing(accelerationProgress, _param.TimeToMaxEasing);
                    break;
                case SteamGearGeneratorState.Running:
                    SteamConsumptionRate = 1f;
                    break;
                case SteamGearGeneratorState.Decelerating:
                    var decelerationProgress = Mathf.Clamp01(StateElapsedTime / _param.TimeToMax);
                    var eased = ApplyEasing(decelerationProgress, _param.TimeToMaxEasing);
                    SteamConsumptionRate = RateAtDecelerationStart * (1f - eased);
                    break;
            }
            
            #region Internal
            
            float ApplyEasing(float t, string easingType)
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
            
            #endregion
        }
        
        
        
        // セーブデータから状態機械の内部値を復元する
        // Restore internal values of the state machine from a save snapshot
        public void Restore(SteamGearGeneratorSaveData saveData)
        {
            CurrentState = Enum.TryParse(saveData.CurrentState, out SteamGearGeneratorState parsedState) ? parsedState : SteamGearGeneratorState.Idle;
            StateElapsedTime = saveData.StateElapsedTime;
            SteamConsumptionRate = saveData.SteamConsumptionRate;
            RateAtDecelerationStart = saveData.RateAtDecelerationStart;
        }
    }
}

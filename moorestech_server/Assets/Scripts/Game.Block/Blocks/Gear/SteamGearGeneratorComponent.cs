using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class SteamGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IUpdatableBlockComponent, IBlockSaveState
    {
        public int TeethCount { get; }
        public RPM GenerateRpm { get; private set; }
        public Torque GenerateTorque { get; private set; }
        public bool GenerateIsClockwise => true;
        
        private readonly SteamGearGeneratorBlockParam _param;
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;
        
        // 状態管理
        private GeneratorState _currentState = GeneratorState.Idle;
        private float _stateElapsedTime = 0f;
        private float _nextConsumptionTime = 0f;
        
        // 出力制御（0〜1の範囲）
        private float _steamConsumptionRate = 0f;
        
        private enum GeneratorState
        {
            Idle,         // 停止中
            Accelerating, // 加速中
            Running,      // 最大出力で稼働中
            Decelerating  // 減速中
        }
        
        public SteamGearGeneratorComponent(
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            SteamGearGeneratorFluidComponent fluidComponent) 
            : base(new Torque(0), blockInstanceId, connectorComponent)
        {
            _param = param;
            _fluidComponent = fluidComponent;
            TeethCount = param.TeethCount;
            GenerateRpm = new RPM(0);
            GenerateTorque = new Torque(0);
            _stateElapsedTime = 0f;
            _nextConsumptionTime = 0f;
        }
        
        public SteamGearGeneratorComponent(
            Dictionary<string, string> componentStates,
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            SteamGearGeneratorFluidComponent fluidComponent) 
            : this(param, blockInstanceId, connectorComponent, fluidComponent)
        {
            if (!componentStates.TryGetValue(SaveKey, out var saveState)) return;
            
            var saveData = JsonConvert.DeserializeObject<SteamGearGeneratorSaveData>(saveState);
            
            if (Enum.TryParse<GeneratorState>(saveData.CurrentState, out var state))
            {
                _currentState = state;
            }
            
            _stateElapsedTime = saveData.StateElapsedTime;
            _nextConsumptionTime = saveData.NextConsumptionTime;
            _steamConsumptionRate = saveData.SteamConsumptionRate;
            
            // 現在の出力を復元
            UpdateOutput();
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            UpdateState();
            UpdateOutput();
            
            // 接続されたギアに動力を供給
            SupplyPower(GenerateRpm, GenerateTorque, GenerateIsClockwise);
        }
        
        private void UpdateState()
        {
            var hasSteam = CheckSteamAvailability();
            var isPipeDisconnected = _fluidComponent.IsPipeDisconnected;
            
            // 経過時間を更新
            _stateElapsedTime += (float)GameUpdater.UpdateSecondTime;
            
            switch (_currentState)
            {
                case GeneratorState.Idle:
                    if (hasSteam)
                    {
                        TransitionToState(GeneratorState.Accelerating);
                    }
                    break;
                    
                case GeneratorState.Accelerating:
                    // 消費タイミングで蒸気を消費
                    TryConsumeSteam();
                    
                    // 最大出力に達したら稼働状態へ
                    if (_steamConsumptionRate >= 1.0f)
                    {
                        TransitionToState(GeneratorState.Running);
                    }
                    break;
                    
                case GeneratorState.Running:
                    // パイプが切断されたら減速開始
                    if (isPipeDisconnected)
                    {
                        Debug.Log($"[SteamGenerator] Pipe disconnected - Starting deceleration from rate: {_steamConsumptionRate:F2}");
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    // 蒸気を消費できなければ減速開始
                    else if (!TryConsumeSteam())
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    break;
                    
                case GeneratorState.Decelerating:
                    // 完全に停止したらアイドル状態へ
                    if (_steamConsumptionRate <= 0f)
                    {
                        TransitionToState(GeneratorState.Idle);
                    }
                    break;
            }
            
            #region Internal
            
            void TransitionToState(GeneratorState newState)
            {
                _currentState = newState;
                _stateElapsedTime = 0f;
            }
            
            
            bool CheckSteamAvailability()
            {
                if (_param.RequiredFluids == null || _param.RequiredFluids.Length == 0)
                {
                    return false;
                }
                
                var requiredFluid = _param.RequiredFluids[0];
                var steamFluidId = MasterHolder.FluidMaster.GetFluidId(requiredFluid.FluidGuid);
                var requiredAmount = requiredFluid.Amount;
                
                // 蒸気タンクから必要量があるかチェック
                var steamTank = _fluidComponent.SteamTank;
                return steamTank.FluidId == steamFluidId && steamTank.Amount >= requiredAmount;
            }
            
            bool TryConsumeSteam()
            {
                if (_param.RequiredFluids == null || _param.RequiredFluids.Length == 0)
                {
                    return false;
                }
                
                var requiredFluid = _param.RequiredFluids[0];
                var steamFluidId = MasterHolder.FluidMaster.GetFluidId(requiredFluid.FluidGuid);
                var requiredAmount = requiredFluid.Amount;
                
                // 蒸気タンクから必要量があるかチェック
                var steamTank = _fluidComponent.SteamTank;
                if (steamTank.FluidId != steamFluidId || steamTank.Amount < requiredAmount)
                {
                    return false;
                }
                
                // 消費時間チェック
                if (_nextConsumptionTime > 0)
                {
                    _nextConsumptionTime -= (float)GameUpdater.UpdateSecondTime;
                    if (_nextConsumptionTime > 0)
                    {
                        return true; // まだ消費タイミングではないが、蒸気は十分にある
                    }
                }
                
                // 蒸気を消費
                steamTank.Amount -= requiredAmount;
                
                // 次の消費時間を設定
                _nextConsumptionTime = requiredFluid.ConsumptionTime;
                
                return true;
            }
            
            #endregion
        }
        
        private void UpdateOutput()
        {
            switch (_currentState)
            {
                case GeneratorState.Idle:
                    _steamConsumptionRate = 0f;
                    break;
                    
                case GeneratorState.Accelerating:
                    // 時間経過に基づいて出力を増加
                    var accelerationProgress = Mathf.Clamp01(_stateElapsedTime / _param.TimeToMax);
                    _steamConsumptionRate = ApplyEasing(accelerationProgress, _param.TimeToMaxEasing);
                    break;
                    
                case GeneratorState.Running:
                    // 最大出力を維持
                    _steamConsumptionRate = 1.0f;
                    break;
                    
                case GeneratorState.Decelerating:
                    // 時間経過に基づいて出力を減少
                    // 最小でも0.01の進行度を確保して、即座に減速が見えるようにする
                    var decelerationProgress = Mathf.Max(0.01f, Mathf.Clamp01(_stateElapsedTime / _param.TimeToMax));
                    var easedProgress = ApplyEasing(decelerationProgress, _param.TimeToMaxEasing);
                    _steamConsumptionRate = 1f - easedProgress;
                    break;
            }
            
            // 消費率に基づいて実際の出力を計算
            GenerateRpm = new RPM(_param.GenerateMaxRpm * _steamConsumptionRate);
            GenerateTorque = new Torque(_param.GenerateMaxTorque * _steamConsumptionRate);
        }
        
        private float ApplyEasing(float t, string easingType)
        {
            switch (easingType)
            {
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.Linear:
                    return t;
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInSine:
                    return 1 - Mathf.Cos((t * Mathf.PI) / 2);
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutSine:
                    return Mathf.Sin((t * Mathf.PI) / 2);
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCubic:
                    return t * t * t;
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCubic:
                    return 1 - Mathf.Pow(1 - t, 3);
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInQuint:
                    return t * t * t * t * t;
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutQuint:
                    return 1 - Mathf.Pow(1 - t, 5);
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCirc:
                    return 1 - Mathf.Sqrt(1 - Mathf.Pow(t, 2));
                    
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCirc:
                    return Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));
                    
                default:
                    return t; // デフォルトはLinear
            }
        }
        
        #region IBlockSaveState
        
        public string SaveKey => "steamGearGenerator";
        
        public string GetSaveState()
        {
            var saveData = new SteamGearGeneratorSaveData
            {
                CurrentState = _currentState.ToString(),
                StateElapsedTime = _stateElapsedTime,
                NextConsumptionTime = _nextConsumptionTime,
                SteamConsumptionRate = _steamConsumptionRate
            };
            
            return JsonConvert.SerializeObject(saveData);
        }
        
        // Save data structure
        private class SteamGearGeneratorSaveData
        {
            public string CurrentState { get; set; }
            public float StateElapsedTime { get; set; }
            public float NextConsumptionTime { get; set; }
            public float SteamConsumptionRate { get; set; }
        }
        
        #endregion
    }
}
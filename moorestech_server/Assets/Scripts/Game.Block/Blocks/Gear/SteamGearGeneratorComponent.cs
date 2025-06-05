using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class SteamGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IUpdatableBlockComponent
    {
        public int TeethCount { get; }
        public RPM GenerateRpm { get; private set; }
        
        public Torque GenerateTorque { get; private set; }
        
        public bool GenerateIsClockwise => true;
        
        private readonly SteamGearGeneratorBlockParam _param;
        private readonly VanillaMachineInputInventory _inputInventory;
        
        private DateTime _startTime;
        private bool _isRunning;
        private float _steamConsumptionRate;
        
        public SteamGearGeneratorComponent(
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            VanillaMachineInputInventory inputInventory) 
            : base(new Torque(0), blockInstanceId, connectorComponent)
        {
            _param = param;
            _inputInventory = inputInventory;
            TeethCount = param.TeethCount;
            GenerateRpm = new RPM(0);
            GenerateTorque = new Torque(0);
            _startTime = DateTime.Now;
            _isRunning = false;
            _steamConsumptionRate = 0f;
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            // 蒸気の消費をチェック
            var steamConsumed = ConsumeSteam();
            
            if (steamConsumed)
            {
                
                if (!_isRunning)
                {
                    _isRunning = true;
                    _startTime = DateTime.Now;
                }
                
                // 時間経過に基づいて出力を更新
                var elapsedTime = (float)(DateTime.Now - _startTime).TotalSeconds;
                var progress = Mathf.Clamp01(elapsedTime / _param.TimeToMax);
                
                // イージング関数を適用
                var easedProgress = ApplyEasing(progress, _param.TimeToMaxEasing);
                
                // 現在のRPMとトルクを計算（蒸気消費率を考慮）
                var rpm = _param.GenerateMaxRpm * easedProgress * _steamConsumptionRate;
                var torque = _param.GenerateMaxTorque * easedProgress * _steamConsumptionRate;
                
                GenerateRpm = new RPM(rpm);
                GenerateTorque = new Torque(torque);
                
                // 接続されたギアに動力を供給
                SupplyPower(GenerateRpm, GenerateTorque, GenerateIsClockwise);
            }
            else
            {
                // 蒸気がない場合は停止
                if (_isRunning)
                {
                    _isRunning = false;
                    GenerateRpm = new RPM(0);
                    GenerateTorque = new Torque(0);
                    
                    // 動力供給を停止
                    SupplyPower(GenerateRpm, GenerateTorque, GenerateIsClockwise);
                }
            }
        }
        
        private bool ConsumeSteam()
        {
            if (_param.RequiredFluids == null || _param.RequiredFluids.Length == 0)
            {
                return false;
            }
            
            var requiredFluid = _param.RequiredFluids[0];
            var steamFluidId = MasterHolder.FluidMaster.GetFluidId(requiredFluid.FluidGuid);
            var requiredAmountPerSecond = requiredFluid.Amount / requiredFluid.ConsumptionTime;
            var requiredAmount = requiredAmountPerSecond * GameUpdater.UpdateSecondTime;
            
            // 最小動作圧力（20%）を考慮
            var minimumAmount = requiredAmount * 0.2f;
            
            // 入力タンクから蒸気を消費
            foreach (var container in _inputInventory.FluidInputSlot)
            {
                if (container.FluidId == steamFluidId && container.Amount >= minimumAmount)
                {
                    // 実際に消費できる量を計算（最大でrequiredAmount）
                    var actualConsumption = Mathf.Min((float)container.Amount, (float)requiredAmount);
                    container.Amount -= actualConsumption;
                    
                    // 消費率を保存（出力計算で使用）
                    _steamConsumptionRate = actualConsumption / (float)requiredAmount;
                    return true;
                }
            }
            
            _steamConsumptionRate = 0f;
            return false;
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
    }
}
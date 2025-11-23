using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public readonly struct GearOverloadConfig
    {
        public GearOverloadConfig(double overloadMaxRpm, double overloadMaxTorque, double destructionCheckInterval, double baseDestructionProbability)
        {
            OverloadMaxRpm = overloadMaxRpm;
            OverloadMaxTorque = overloadMaxTorque;
            DestructionCheckInterval = destructionCheckInterval;
            BaseDestructionProbability = baseDestructionProbability;
        }

        public double OverloadMaxRpm { get; }
        public double OverloadMaxTorque { get; }
        public double DestructionCheckInterval { get; }
        public double BaseDestructionProbability { get; }
        public bool IsActive => BaseDestructionProbability > 0 && (OverloadMaxRpm > 0 || OverloadMaxTorque > 0);

        public static GearOverloadConfig From(IGearOverloadParam param)
        {
            return new GearOverloadConfig(param.OverloadMaxRpm, param.OverloadMaxTorque, param.DestructionCheckInterval, param.BaseDestructionProbability);
        }
    }
    
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateObservable, IUpdatableBlockComponent
    {
        public IObservable<Unit> OnChangeBlockState => _simpleGearService.BlockStateChange;
        public IObservable<GearUpdateType> OnGearUpdate => _simpleGearService.OnGearUpdate;
        
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;

        public bool IsDestroy { get; private set; }
        
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly GearOverloadConfig _overloadConfig;
        private readonly IBlockRemover _blockRemover;
        private readonly double _checkInterval;
        private readonly bool _overloadEnabled;
        private double _elapsedSeconds;
        
        protected readonly Torque RequiredTorque;
        private readonly SimpleGearService _simpleGearService;
        
        public GearEnergyTransformer(Torque requiredTorque, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, GearOverloadConfig overloadConfig, IBlockRemover blockRemover)
        {
            RequiredTorque = requiredTorque;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService(blockInstanceId);
            _overloadConfig = overloadConfig;
            _blockRemover = blockRemover;
            _checkInterval = Math.Max(overloadConfig.DestructionCheckInterval, 0.001f);
            _overloadEnabled = overloadConfig.IsActive;
            
            GearNetworkDatastore.AddGear(this);
        }
        
        public BlockStateDetail[] GetBlockStateDetails()
        {
            return new []{ _simpleGearService.GetBlockStateDetail() };
        }
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            return RequiredTorque;
        }
        
        public void StopNetwork()
        {
            _simpleGearService.StopNetwork();
        }
        
        public virtual void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            _simpleGearService.SupplyPower(rpm, torque, isClockwise);
        }
        
        public List<GearConnect> GetGearConnects()
        {
            var result = new List<GearConnect>();
            foreach (var target in _connectorComponent.ConnectedTargets)
            {
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.SelfOption, (GearConnectOption)target.Value.TargetOption));
            }
            return result;
        }
        
        public virtual void Update()
        {
            // 過負荷判定のインターバルを管理
            // Manage interval for overload checks
            if (!_overloadEnabled || IsDestroy) return;

            _elapsedSeconds += GameUpdater.UpdateSecondTime;
            if (_elapsedSeconds < _checkInterval) return;
            _elapsedSeconds = 0;

            // 過負荷時の破壊確率を計算し、抽選する
            // Calculate destruction probability when overloaded and roll
            var chance = CalculateDestructionProbability();
            if (chance <= 0f) return;
            if (UnityEngine.Random.value <= chance) RequestRemove();

            #region Internal

            float CalculateDestructionProbability()
            {
                var rpmRatio = _overloadConfig.OverloadMaxRpm > 0 ? CurrentRpm.AsPrimitive() / (float)_overloadConfig.OverloadMaxRpm : 0f;
                var torqueRatio = _overloadConfig.OverloadMaxTorque > 0 ? CurrentTorque.AsPrimitive() / (float)_overloadConfig.OverloadMaxTorque : 0f;
                var rpmExcess = rpmRatio > 1f ? rpmRatio : 0f;
                var torqueExcess = torqueRatio > 1f ? torqueRatio : 0f;
                if (rpmExcess <= 0f && torqueExcess <= 0f) return 0f;

                var multiplier = rpmExcess > 0f && torqueExcess > 0f ? rpmExcess * torqueExcess : Math.Max(rpmExcess, torqueExcess);
                var probability = (float)(_overloadConfig.BaseDestructionProbability * multiplier);
                return Mathf.Clamp01(probability);
            }

            void RequestRemove()
            {
                _blockRemover.Remove(BlockInstanceId, BlockRemoveReason.Broken);
            }

            #endregion
        }
        
        public void Destroy()
        {
            IsDestroy = true;
            GearNetworkDatastore.RemoveGear(this);
            _simpleGearService.Destroy();
        }
    }
    
    public static class GearEnergyTransformerExtension
    {
        public static ElectricPower CalcMachineSupplyPower(this GearEnergyTransformer energyTransformer, RPM requiredRpm, Torque requiredTorque)
        {
            var currentRpm = energyTransformer.CurrentRpm;
            var currentTorque = energyTransformer.CurrentTorque;
            
            var rpmRate = Mathf.Min((currentRpm / requiredRpm).AsPrimitive(), 1);
            var torqueRate = Mathf.Min((currentTorque / requiredTorque).AsPrimitive(), 1);
            var powerRate = rpmRate * torqueRate;
            
            var requiredGearPower = requiredRpm.AsPrimitive() * requiredTorque.AsPrimitive();
            var currentElectricPower = new ElectricPower(requiredGearPower * powerRate);
            
            return currentElectricPower;
        }
    }
}

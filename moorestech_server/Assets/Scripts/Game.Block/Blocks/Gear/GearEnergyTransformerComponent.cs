using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateObservable, IUpdatableBlockComponent
    {
        public IObservable<Unit> OnChangeBlockState => _simpleGearService.BlockStateChange;
        public IObservable<GearUpdateType> OnGearUpdate => _simpleGearService.OnGearUpdate;
        
        public BlockInstanceId BlockInstanceId { get; }
        public virtual RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public virtual Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;

        public bool IsDestroy { get; private set; }
        
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        
        protected readonly Torque RequiredTorque;
        private readonly SimpleGearService _simpleGearService;
        
        private readonly IBlockRemover _blockRemover;
        private readonly GearOverloadConfig _overloadConfig;
        private float _timeSinceLastCheck;
        private readonly System.Random _random = new();
        
        public GearEnergyTransformer(
            Torque requiredTorque, 
            GearOverloadConfig overloadConfig,
            IBlockRemover blockRemover,
            BlockInstanceId blockInstanceId, 
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            RequiredTorque = requiredTorque;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService(blockInstanceId);
            
            _overloadConfig = overloadConfig;
            _blockRemover = blockRemover;
            
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
            if (IsDestroy) return;
            
            _timeSinceLastCheck += Time.deltaTime;
            if (_timeSinceLastCheck < _overloadConfig.CheckInterval) return;
            _timeSinceLastCheck = 0;
            
            CheckOverload();
        }

        private void CheckOverload()
        {
            if (_overloadConfig.MaxRpm <= 0 && _overloadConfig.MaxTorque <= 0) return;
            
            float rpmRatio = 0;
            if (_overloadConfig.MaxRpm > 0 && CurrentRpm.AsPrimitive() > _overloadConfig.MaxRpm)
            {
                rpmRatio = CurrentRpm.AsPrimitive() / _overloadConfig.MaxRpm;
            }
            
            float torqueRatio = 0;
            if (_overloadConfig.MaxTorque > 0 && CurrentTorque.AsPrimitive() > _overloadConfig.MaxTorque)
            {
                torqueRatio = CurrentTorque.AsPrimitive() / _overloadConfig.MaxTorque;
            }
            
            if (rpmRatio <= 1.0f && torqueRatio <= 1.0f) return;
            
            float totalRatio = 1.0f;
            if (rpmRatio > 1.0f) totalRatio *= rpmRatio;
            if (torqueRatio > 1.0f) totalRatio *= torqueRatio;
            
            float probability = _overloadConfig.BaseProb * totalRatio;
            
            if (_random.NextDouble() < probability)
            {
                _blockRemover.Remove(BlockInstanceId, BlockRemoveReason.Broken);
            }
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

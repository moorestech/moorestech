using System;
using System.Collections.Generic;
using System.Linq;
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
    
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _simpleGearService.BlockStateChange;
        public IObservable<GearUpdateType> OnGearUpdate => _simpleGearService.OnGearUpdate;
        
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;

        public bool IsDestroy { get; private set; }
        
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        
        protected readonly Torque RequiredTorque;
        private readonly SimpleGearService _simpleGearService;
        
        public GearEnergyTransformer(Torque requiredTorque, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            RequiredTorque = requiredTorque;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService(blockInstanceId);
            
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

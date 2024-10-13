using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateChange
    {
        public IObservable<BlockState> OnChangeBlockState => _simpleGearService.BlockStateChange;
        public IObservable<GearUpdateType> OnGearUpdate => _simpleGearService.OnGearUpdate;
        
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;
        
        public bool IsRocked => _simpleGearService.IsRocked;
        public bool IsDestroy { get; private set; }
        
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        
        private readonly Torque _requiredTorque;
        private readonly SimpleGearService _simpleGearService;
        
        public GearEnergyTransformer(Torque requiredTorque, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            _requiredTorque = requiredTorque;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();
            
            GearNetworkDatastore.AddGear(this);
        }
        
        public BlockState GetBlockState()
        {
            return _simpleGearService.GetBlockState();
        }
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            return _requiredTorque;
        }
        
        public void Rocked()
        {
            _simpleGearService.Rocked();
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
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.selfOption, (GearConnectOption)target.Value.targetOption));
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
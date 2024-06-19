using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateChange
    {
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
        
        public IObservable<BlockState> OnChangeBlockState => _simpleGearService.BlockStateChange;
        
        public BlockState GetBlockState()
        {
            return _simpleGearService.GetBlockState();
        }
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;
        
        public bool IsRocked => _simpleGearService.IsRocked;
        
        public bool IsDestroy { get; private set; }
        
        public IReadOnlyList<GearConnect> Connects =>
            _connectorComponent.ConnectedTargets.Select(
                target => new GearConnect(target.Key, (GearConnectOption)target.Value.selfOption, (GearConnectOption)target.Value.targetOption)
            ).ToArray();
        
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
        
        public void Destroy()
        {
            IsDestroy = true;
            GearNetworkDatastore.RemoveGear(this);
            _simpleGearService.Destroy();
        }
    }
}
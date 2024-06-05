using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateChange
    {
        public int EntityId { get; }
        public float CurrentRpm => _simpleGearService.CurrentRpm;
        public float CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;
        public bool IsRocked => _simpleGearService.IsRocked;
        
        public IObservable<BlockState> OnChangeBlockState => _simpleGearService.BlockStateChange;
        
        public IReadOnlyList<GearConnect> Connects =>
            _connectorComponent.ConnectTargets.Select(
                target => new GearConnect(
                    target.Key,
                    (GearConnectOption)target.Value.selfOption,
                    (GearConnectOption)target.Value.targetOption
                )
            ).ToArray();
        
        public virtual float RequiredPower { get; }
        
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly SimpleGearService _simpleGearService;
        
        public GearEnergyTransformer(float requiredPower, int entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            RequiredPower = requiredPower;
            EntityId = entityId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();
            
            GearNetworkDatastore.AddGear(this);
        }
        
        public BlockState GetBlockState()
        {
            return _simpleGearService.GetBlockState();
        }
        
        public virtual void Rocked()
        {
            _simpleGearService.Rocked();
        }
        
        public virtual void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            _simpleGearService.SupplyPower(rpm, torque, isClockwise);
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
            GearNetworkDatastore.RemoveGear(this);
            _simpleGearService.Destroy();
        }
    }
}
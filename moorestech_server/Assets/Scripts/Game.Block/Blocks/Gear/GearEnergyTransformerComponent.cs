using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Gear.Common;

namespace Game.Block.Blocks.Gear
{
    public class GearEnergyTransformer : IGearEnergyTransformer, IBlockStateChange
    {
        public int EntityId { get; }
        public bool IsReverseRotation { get; }
        public float RequiredPower { get; }
        public IReadOnlyList<IGearEnergyTransformer> ConnectingTransformers => _connectorComponent.ConnectTargets;
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;

        public IObservable<ChangedBlockState> BlockStateChange => _simpleGearService.BlockStateChange;

        private readonly SimpleGearService _simpleGearService;


        public float CurrentRpm => _simpleGearService.CurrentRpm;
        public float CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;

        public bool IsDestroy { get; private set; }

        public GearEnergyTransformer(float lossPower, int entityId, bool isReverseRotation, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            RequiredPower = lossPower;
            EntityId = entityId;
            _connectorComponent = connectorComponent;
            IsReverseRotation = isReverseRotation;
            _simpleGearService = new SimpleGearService();
        }

        public void Rocked() { _simpleGearService.Rocked(); }

        public void SupplyPower(float rpm, float torque, bool isClockwise) { _simpleGearService.SupplyPower(rpm, torque, isClockwise); }

        public void Destroy()
        {
            IsDestroy = false;
            _simpleGearService.Destroy();
        }
    }
}
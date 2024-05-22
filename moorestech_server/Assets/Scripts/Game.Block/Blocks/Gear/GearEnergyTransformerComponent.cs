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
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        private readonly SimpleGearService _simpleGearService;

        public GearEnergyTransformer(float lossPower, int entityId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            RequiredPower = lossPower;
            EntityId = entityId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();

            GearNetworkDatastore.AddGear(this);
        }

        public IObservable<ChangedBlockState> BlockStateChange => _simpleGearService.BlockStateChange;
        public int EntityId { get; }
        public float RequiredPower { get; }

        public IReadOnlyList<GearConnect> Connects =>
            _connectorComponent.ConnectTargets.Select(
                target => new GearConnect(
                    target.Key,
                    (GearConnectOption)target.Value.selfOption,
                    (GearConnectOption)target.Value.targetOption
                )
            ).ToArray();

        public float CurrentRpm => _simpleGearService.CurrentRpm;
        public float CurrentTorque => _simpleGearService.CurrentTorque;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;

        public bool IsDestroy { get; private set; }

        public void Rocked() { _simpleGearService.Rocked(); }

        public void SupplyPower(float rpm, float torque, bool isClockwise) { _simpleGearService.SupplyPower(rpm, torque, isClockwise); }

        public void Destroy()
        {
            IsDestroy = true;
            GearNetworkDatastore.RemoveGear(this);
            _simpleGearService.Destroy();
        }
    }
}
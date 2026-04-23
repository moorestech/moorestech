using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.GearConsumptionModule;
using UniRx;

namespace Game.Block.Blocks.Gear
{
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

        protected readonly GearConsumption Consumption;
        private readonly SimpleGearService _simpleGearService;

        public GearEnergyTransformer(GearConsumption consumption, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            Consumption = consumption;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();

            GearNetworkDatastore.AddGear(this);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            return new[] { _simpleGearService.GetBlockStateDetail() };
        }

        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // 生成側（Generator）はConsumption=nullで常にトルク消費0
            // Generators pass null Consumption and always consume zero torque
            if (Consumption == null) return new Torque(0);
            return GearConsumptionCalculator.CalcRequiredTorque(Consumption, rpm);
        }

        // 現在のRPM/トルクに対する出力倍率。出力系コンポーネント（Machine/Miner/Pump/Conveyor/ElectricGen）から参照される
        // Output scaling rate for the current RPM/torque. Referenced by output-side components.
        public float CurrentOperatingRate =>
            Consumption == null ? 0f : GearConsumptionCalculator.CalcOperatingRate(Consumption, CurrentRpm, CurrentTorque);

        public virtual void StopNetwork()
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
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.SelfConnector?.ConnectOption, (GearConnectOption)target.Value.TargetConnector?.ConnectOption));
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
}

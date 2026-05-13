using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
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

        private readonly GearConsumption _consumption;
        private readonly SimpleGearService _simpleGearService;

        public GearEnergyTransformer(GearConsumption consumption, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent)
        {
            _consumption = consumption;
            BlockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();

            GearNetworkDatastore.AddGear(this);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            return new[] { _simpleGearService.GetBlockStateDetail() };
        }

        public virtual Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // 生成側（Generator）はConsumption=nullで常にトルク消費0
            // Generators pass null Consumption and always consume zero torque
            if (_consumption == null) return new Torque(0);
            return GearConsumptionCalculator.CalcRequiredTorque(_consumption, rpm);
        }

        // 現在のRPM/トルクに対する出力倍率。出力系コンポーネント（Machine/Miner/Pump/Conveyor/ElectricGen）から参照される
        // Output scaling rate for the current RPM/torque. Referenced by output-side components.
        public virtual float GetCurrentOperatingRate()
        {
            return _consumption == null ? 0f : GearConsumptionCalculator.CalcOperatingRate(_consumption, CurrentRpm, CurrentTorque);
        }

        // 基準電力（baseTorque × baseRpm）に稼働率を乗じた現在の供給電力。Machine/Miner系へ渡す共通計算
        // Current supplied power = basePower × operatingRate. Shared calc for Machine/Miner components.
        public virtual ElectricPower GetCurrentSuppliedPower()
        {
            return _consumption == null ? new ElectricPower(0) : GearConsumptionCalculator.CalcCurrentPower(_consumption, GetCurrentOperatingRate());
        }

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

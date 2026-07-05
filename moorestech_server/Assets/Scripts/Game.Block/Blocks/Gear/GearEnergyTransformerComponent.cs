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

        // 現在値は保持せず、所属networkの符号付き原点RPM比×原点RPMから毎回導出する
        // Current values are not stored; they are derived each time from the owning network's signed ratio × origin RPM
        public RPM CurrentRpm
        {
            get
            {
                TryResolveRotation(out var rpm, out _);
                return rpm;
            }
        }

        public Torque CurrentTorque
        {
            get
            {
                if (!TryResolveRotation(out var rpm, out var isClockwise)) return new Torque(0);
                if (rpm.AsPrimitive() <= 0f) return new Torque(0);

                // generatorは自身の発電トルク、消費側は現在RPMでの要求トルクを現在トルクとみなす（供給十分＝要求満額のため）
                // A generator reports its generated torque; a consumer's current torque is its required torque at the current RPM (supply is full when running)
                if (this is IGearGenerator generator) return generator.GenerateTorque;
                return GetRequiredTorque(rpm, isClockwise);
            }
        }

        public bool IsCurrentClockwise
        {
            get
            {
                TryResolveRotation(out _, out var isClockwise);
                return isClockwise;
            }
        }

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
            return new[] { _simpleGearService.GetBlockStateDetail(CurrentRpm, CurrentTorque, IsCurrentClockwise) };
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

        public void NotifyStateChanged()
        {
            _simpleGearService.NotifyStateChanged();
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

        // 所属networkを引き、符号付き原点RPM比から実RPMと絶対回転方向を導出する
        // Look up the owning network and derive actual RPM and absolute direction from the signed origin RPM ratio
        private bool TryResolveRotation(out RPM rpm, out bool isClockwise)
        {
            rpm = new RPM(0);
            isClockwise = true;
            if (!GearNetworkDatastore.TryGetGearNetwork(BlockInstanceId, out var network)) return false;
            return network.TryResolveRotation(BlockInstanceId, out rpm, out isClockwise);
        }
    }
}

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

        // 消費倍率の供給源（未設定は中立1.0）
        // Consumption multiplier source (neutral 1.0 when unset)
        private IConsumptionMultiplierSource _consumptionMultiplierSource;
        private float ConsumptionMultiplier => _consumptionMultiplierSource?.ConsumptionMultiplier ?? 1f;

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

            // 要求トルクに消費倍率を乗じる
            // Scale the required torque by the multiplier
            var baseTorque = GearConsumptionCalculator.CalcRequiredTorque(_consumption, rpm);
            return new Torque(baseTorque.AsPrimitive() * ConsumptionMultiplier);
        }

        // 倍率は照会毎に読むため付け外しが即時反映
        // Read per query, so module changes apply immediately
        public void SetConsumptionMultiplierSource(IConsumptionMultiplierSource consumptionMultiplierSource)
        {
            _consumptionMultiplierSource = consumptionMultiplierSource;
        }

        // 現在のRPM/トルクに対する出力倍率。出力系コンポーネント（Machine/Miner/Pump/Conveyor/ElectricGen）から参照される
        // Output scaling rate for the current RPM/torque. Referenced by output-side components.
        public virtual float GetCurrentOperatingRate()
        {
            if (_consumption == null) return 0f;

            // 倍率適用後の要求トルクで稼働率を計算し整合させる
            // Rate against the scaled demand keeps supply consistent
            var adjustedRequiredTorque = GetRequiredTorque(CurrentRpm, IsCurrentClockwise);
            return GearConsumptionCalculator.CalcOperatingRate(_consumption, CurrentRpm, CurrentTorque, adjustedRequiredTorque);
        }

        // 基準電力（baseTorque × baseRpm）に消費倍率と稼働率を乗じた現在の供給電力。Machine/Miner系へ渡す共通計算
        // Current supplied power = basePower × consumption multiplier × operatingRate. Shared calc for Machine/Miner components.
        public virtual ElectricPower GetCurrentSuppliedPower()
        {
            if (_consumption == null) return new ElectricPower(0);

            // 供給電力にも倍率を乗じ時間短縮の自己相殺を防ぐ
            // Scale supplied power too so speed gains do not self-cancel
            var basePower = GearConsumptionCalculator.CalcCurrentPower(_consumption, GetCurrentOperatingRate());
            return new ElectricPower(basePower.AsPrimitive() * ConsumptionMultiplier);
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

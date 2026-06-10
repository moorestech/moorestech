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

        // 消費倍率プロバイダ。機械の省エネモジュール等が要求トルクを変えるために差し込む（既定は中立1.0）
        // Consumption multiplier provider; machines inject this (e.g. efficiency modules). Defaults to neutral 1.0
        private Func<float> _consumptionMultiplier = () => 1f;

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

            // 基準の要求トルクに消費倍率を乗じる（省エネモジュール未装着・非機械は中立1.0で従来と同値）
            // Multiply the base required torque by the consumption multiplier (neutral 1.0 keeps non-machine gears unchanged)
            var baseTorque = GearConsumptionCalculator.CalcRequiredTorque(_consumption, rpm);
            return new Torque(baseTorque.AsPrimitive() * _consumptionMultiplier());
        }

        // 消費倍率プロバイダを差し込む。倍率はトルク照会時に都度読むため、加工スナップショットの変化が即時反映される
        // Inject the consumption multiplier provider; it is read lazily on each torque query, so snapshot changes apply immediately
        public void SetConsumptionMultiplier(Func<float> consumptionMultiplier)
        {
            _consumptionMultiplier = consumptionMultiplier;
        }

        // 現在のRPM/トルクに対する出力倍率。出力系コンポーネント（Machine/Miner/Pump/Conveyor/ElectricGen）から参照される
        // Output scaling rate for the current RPM/torque. Referenced by output-side components.
        public virtual float GetCurrentOperatingRate()
        {
            if (_consumption == null) return 0f;

            // 倍率適用後の要求トルク（GetRequiredTorqueと同値）に対して稼働率を計算し、要求側と供給側を整合させる（中立1.0は従来計算と同値）
            // Compute the rate against the multiplier-adjusted required torque (same as GetRequiredTorque) so supply stays consistent with demand (neutral 1.0 matches the legacy calc)
            var adjustedRequiredTorque = GetRequiredTorque(CurrentRpm, IsCurrentClockwise);
            return GearConsumptionCalculator.CalcOperatingRate(_consumption, CurrentRpm, CurrentTorque, adjustedRequiredTorque);
        }

        // 基準電力（baseTorque × baseRpm）に消費倍率と稼働率を乗じた現在の供給電力。Machine/Miner系へ渡す共通計算
        // Current supplied power = basePower × consumption multiplier × operatingRate. Shared calc for Machine/Miner components.
        public virtual ElectricPower GetCurrentSuppliedPower()
        {
            if (_consumption == null) return new ElectricPower(0);

            // 倍率分のトルクが供給されていれば有効要求電力（基準×倍率）に一致し、速度モジュールの時間短縮が自己相殺しない
            // With the scaled torque supplied this equals the effective request power (base × multiplier), so speed module time reduction does not self-cancel
            var basePower = GearConsumptionCalculator.CalcCurrentPower(_consumption, GetCurrentOperatingRate());
            return new ElectricPower(basePower.AsPrimitive() * _consumptionMultiplier());
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

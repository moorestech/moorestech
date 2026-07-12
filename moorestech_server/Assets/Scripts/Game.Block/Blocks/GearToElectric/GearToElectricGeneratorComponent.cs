using System;
using System.Collections.Generic;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.GearToElectric
{
    /// <summary>
    ///     歯車エネルギーを電力へ変換する変換機。tick系境界にmaxGeneratedPowerの1tick分の内部バッテリーを持つ。
    ///     歯車tickで不足分だけ充電し、次の電力tickで残量を供給可能電力として申告、統計確定後に利用率に応じて放電する。
    ///     Converter turning gear energy into electric power, holding a one-tick internal battery of maxGeneratedPower at the tick boundary.
    ///     It charges only the deficit on the gear tick, declares the remainder as available supply on the next electric tick, and discharges by utilization after the statistics settle.
    /// </summary>
    public class GearToElectricGeneratorComponent : GearEnergyTransformer, IGear, IElectricGenerator, IElectricTickPostHandler, IUpdatableBlockComponent, IBlockStateDetail, IBlockSaveState
    {
        public int TeethCount => _param.TeethCount;
        public string SaveKey => "gearToElectricGenerator";

        private readonly GearToElectricGeneratorBlockParam _param;

        // バッテリー容量はマスターデータ由来のmaxGeneratedPower（1tick分）。セーブには残量のみ保存する
        // Battery capacity is master-data maxGeneratedPower (one tick's worth); only the remainder is saved
        private float BatteryCapacity => _param.MaxGeneratedPower;
        private float _batteryRemaining;
        private float _lastDischargedPower;

        public GearToElectricGeneratorComponent(
            GearToElectricGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(param.GearConsumption, blockInstanceId, connectorComponent)
        {
            _param = param;
            _batteryRemaining = 0f;
        }

        // セーブ復元用コンストラクタ。残量を0から容量の範囲へクランプして復元する
        // Restore constructor; the battery remainder is clamped into [0, capacity]
        public GearToElectricGeneratorComponent(
            Dictionary<string, string> componentStates,
            GearToElectricGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            this(param, blockInstanceId, connectorComponent)
        {
            if (componentStates == null || !componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saveData = JsonConvert.DeserializeObject<GearToElectricGeneratorSaveJsonObject>(raw);
            if (saveData == null) return;
            _batteryRemaining = Mathf.Clamp(saveData.BatteryRemaining, 0f, BatteryCapacity);
        }

        // 電力tick: 現在のバッテリー残量をこのtickの供給可能電力として申告する
        // Electric tick: declare the current battery remainder as this tick's available supply
        public ElectricPower OutputEnergy()
        {
            BlockException.CheckDestroy(this);
            return new ElectricPower(_batteryRemaining);
        }

        // 電力統計確定後: 消費された割合（利用率）に応じてバッテリーを放電し、不足分比例の要求トルクを歯車側へ伝える
        // After statistics settle: discharge the battery by the consumed fraction (utilization) and push the deficit-proportional torque request to the gear side
        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            DischargeByUtilization();
            UpdateTorqueRequest();

            #region Internal

            void DischargeByUtilization()
            {
                if (statistics.TotalGeneratePower <= 0f) return;
                var deliveredPower = statistics.TotalRequiredPower * statistics.PowerRate;
                var utilization = Mathf.Clamp01(deliveredPower / statistics.TotalGeneratePower);
                _lastDischargedPower = _batteryRemaining * utilization;
                _batteryRemaining -= _lastDischargedPower;
            }

            void UpdateTorqueRequest()
            {
                // 必要な電力の分だけ歯車エネルギーを吸収するため、要求トルクを不足分比例にする
                // Scale the torque request to the deficit so only the needed power is absorbed from the gears
                var deficitRate = BatteryCapacity <= 0f ? 0f : (BatteryCapacity - _batteryRemaining) / BatteryCapacity;
                SetTorqueRequestRate(deficitRate);
            }

            #endregion
        }

        // ブロック更新(歯車tick後): 稼働率に応じて不足分を充電する
        // Block update (after the gear tick): charge the deficit according to the operating rate
        public void Update()
        {
            BlockException.CheckDestroy(this);

            ChargeFromGear();

            // 電線未接続で電力tick後処理が呼ばれない場合でも、満充電後は歯車網へ負荷をかけ続けないよう要求を更新する
            // Refresh the torque request even when no wire connects (so the post-tick hook never runs); a full battery must stop loading the gear network
            var postChargeDeficit = BatteryCapacity - _batteryRemaining;
            SetTorqueRequestRate(BatteryCapacity <= 0f ? 0f : postChargeDeficit / BatteryCapacity);

            #region Internal

            void ChargeFromGear()
            {
                var deficit = BatteryCapacity - _batteryRemaining;
                if (deficit <= 0f) return;

                // 二値変換: 稼働率は1でクランプしオーバードライブ発電を廃止。minimumRpm未満・RPM0は稼働率0で充電しない
                // Binary conversion: the operating rate is clamped at 1 (no overdrive); below minimumRpm or at RPM 0 the rate is 0 and nothing charges
                var operatingRate = Mathf.Clamp01(GetCurrentOperatingRate());
                if (operatingRate <= 0f) return;

                var charge = Mathf.Min(deficit, BatteryCapacity * operatingRate);
                _batteryRemaining = Mathf.Min(BatteryCapacity, _batteryRemaining + charge);
            }

            #endregion
        }

        public new BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var baseDetails = base.GetBlockStateDetails();
            var result = new BlockStateDetail[baseDetails.Length + 1];
            result[0] = CreateDetail();
            Array.Copy(baseDetails, 0, result, 1, baseDetails.Length);
            return result;

            #region Internal

            BlockStateDetail CreateDetail()
            {
                var chargeRate = BatteryCapacity <= 0f ? 0f : _batteryRemaining / BatteryCapacity;
                var detail = new GearToElectricGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
                    chargeRate,
                    new ElectricPower(_lastDischargedPower),
                    _batteryRemaining);
                var serialized = MessagePackSerializer.Serialize(detail);
                return new BlockStateDetail(GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey, serialized);
            }

            #endregion
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return JsonConvert.SerializeObject(new GearToElectricGeneratorSaveJsonObject { BatteryRemaining = _batteryRemaining });
        }
    }

    public class GearToElectricGeneratorSaveJsonObject
    {
        [JsonProperty("batteryRemaining")]
        public float BatteryRemaining;
    }
}

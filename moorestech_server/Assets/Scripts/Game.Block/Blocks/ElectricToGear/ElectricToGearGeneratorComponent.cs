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
using UniRx;
using UnityEngine;
using Game.Context;

namespace Game.Block.Blocks.ElectricToGear
{
    /// <summary>
    ///     電力を歯車回転へ変換する変換機。選択モードのrequiredPower 1tick分の内部バッテリーを持つ。
    ///     電力tickで充電し、満充電のときのみ定格RPM・定格トルクを出力、出力tickで残量を0にする（電力不足時は脈動出力）。
    ///     Converter turning electric power into gear rotation, holding a one-tick battery of the selected mode's requiredPower.
    ///     It charges on the electric tick, outputs the rated RPM and torque only when fully charged, and empties the battery on the output tick (pulsing under power shortage).
    /// </summary>
    public class ElectricToGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IElectricConsumer, IElectricTickPostHandler, IBlockStateDetail, IBlockSaveState, IBlockStateObservable
    {
        private const float FullChargeToleranceRate = 1e-4f;

        public int TeethCount => _param.TeethCount;
        public bool GenerateIsClockwise => true;

        // 満充電確定時のみ定格を出力。トルクドループは行わない。トルク0モードは網の最速起点を奪わないようRPMも0
        // Output the rated values only when settled as fully charged; no torque droop. A torque-0 mode also yields RPM 0 so it never dominates the network
        public RPM GenerateRpm => _isOutputting && 0 < CurrentMode.Torque ? new RPM(CurrentMode.Rpm) : new RPM(0);
        public Torque GenerateTorque => _isOutputting ? new Torque(CurrentMode.Torque) : new Torque(0);

        // 出力tickでバッテリーを毎tick消費するため常時tick駆動が必要
        // Needs continuous ticking since the battery is consumed on every output tick
        public bool RequiresContinuousTick => true;

        public string SaveKey => "electricToGearGenerator";

        // 満充電までに不足している電力だけを要求する
        // Demand only the power still missing from the one-tick battery
        public ElectricPower RequestEnergy => new(Mathf.Max(0f, BatteryCapacity - _batteryRemaining));

        public new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        public int SelectedIndex { get; private set; }

        private readonly ElectricToGearGeneratorBlockParam _param;
        private readonly Subject<Unit> _onChangeBlockState = new();

        // 基底（ギア網）の状態変化を自前のSubjectへ転送する購読。基底Destroyに破棄フックが無いため明示破棄はしない
        // Subscription forwarding base (gear-network) state changes into our Subject; not disposed explicitly since base Destroy has no hook
        private readonly IDisposable _baseStateForward;

        private float _batteryRemaining;
        private float _lastChargedPower;
        private bool _isOutputting;

        private OutputModesElement CurrentMode => _param.OutputModes[SelectedIndex];

        // バッテリー容量は選択モードのrequiredPower 1tick分。マスターデータから再構築されセーブには残量のみ保存する
        // Battery capacity is one tick of the selected mode's requiredPower, rebuilt from master data; only the remainder is saved
        private float BatteryCapacity => CurrentMode.RequiredPower;

        public ElectricToGearGeneratorComponent(
            ElectricToGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(null, blockInstanceId, connectorComponent)
        {
            _param = param;
            SelectedIndex = 0;
            _batteryRemaining = 0f;
            _baseStateForward = base.OnChangeBlockState.Subscribe(_ => _onChangeBlockState.OnNext(Unit.Default));
        }

        // セーブ復元用コンストラクタ。indexは範囲へ、バッテリー残量は0から容量の範囲へクランプする
        // Restore constructor; the index is clamped into range and the battery remainder into [0, capacity]
        public ElectricToGearGeneratorComponent(
            Dictionary<string, string> componentStates,
            ElectricToGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            this(param, blockInstanceId, connectorComponent)
        {
            if (componentStates == null || !componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saveData = JsonConvert.DeserializeObject<ElectricToGearGeneratorSaveJsonObject>(raw);
            if (saveData == null) return;
            SelectedIndex = Mathf.Clamp(saveData.SelectedIndex, 0, _param.OutputModes.Length - 1);
            _batteryRemaining = Mathf.Clamp(saveData.BatteryRemaining, 0f, BatteryCapacity);
        }

        // 電力tick後処理: 供給率に応じて充電し、満充電なら次の歯車tickの出力を確定する
        // Electric post-tick: charge by the supply rate, then settle the output for the following gear tick when full
        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            _lastChargedPower = Mathf.Min(RequestEnergy.AsPrimitive() * statistics.PowerRate, BatteryCapacity - _batteryRemaining);
            _batteryRemaining += _lastChargedPower;

            // 浮動小数の充電誤差を許容して満充電を判定する
            // Full charge is judged with a small tolerance for floating point charge error
            var isFull = BatteryCapacity - BatteryCapacity * FullChargeToleranceRate <= _batteryRemaining;
            SetOutputting(isFull);
            if (0f < _lastChargedPower) _onChangeBlockState.OnNext(Unit.Default);
        }

        // 出力tick: 1tick分のバッテリーを全て消費して定格出力し、残量を0にする
        // Output tick: consume the whole one-tick battery for the rated output, leaving the remainder at 0
        public void ConsumeGeneratorTick(float networkLoadRate)
        {
            BlockException.CheckDestroy(this);
            if (!_isOutputting) return;

            _batteryRemaining = 0f;
            SetOutputting(false);
        }

        // 出力モード切替。容量が変わるため残量を新容量へクランプする
        // Switch the output mode; the battery is clamped to the new capacity since it changes
        public bool SetSelectedMode(int index)
        {
            BlockException.CheckDestroy(this);
            if (index < 0 || index >= _param.OutputModes.Length) return false;
            var changed = SelectedIndex != index;
            SelectedIndex = index;
            _batteryRemaining = Mathf.Clamp(_batteryRemaining, 0f, BatteryCapacity);

            if (changed)
            {
                ServerContext.GetService<IGearNetworkDatastore>().NotifyGeneratorOutputChanged(this);
                _onChangeBlockState.OnNext(Unit.Default);
            }
            return true;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return JsonConvert.SerializeObject(new ElectricToGearGeneratorSaveJsonObject
            {
                SelectedIndex = SelectedIndex,
                BatteryRemaining = _batteryRemaining,
            });
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
                var detail = new ElectricToGearGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
                    SelectedIndex,
                    chargeRate,
                    new ElectricPower(_lastChargedPower),
                    _batteryRemaining);
                var serialized = MessagePackSerializer.Serialize(detail);
                return new BlockStateDetail(ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey, serialized);
            }

            #endregion
        }

        // 出力確定状態の反転時のみ歯車網へ再計算を要求し、クライアントへも通知する
        // Only when the settled output state flips, request the gear network recalculation and notify the client
        private void SetOutputting(bool isOutputting)
        {
            if (_isOutputting == isOutputting) return;
            _isOutputting = isOutputting;
            ServerContext.GetService<IGearNetworkDatastore>().NotifyGeneratorOutputChanged(this);
            _onChangeBlockState.OnNext(Unit.Default);
        }
    }
}

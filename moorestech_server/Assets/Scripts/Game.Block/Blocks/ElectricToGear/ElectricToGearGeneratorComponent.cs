using System;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.ElectricToGear
{
    public class ElectricToGearGeneratorComponent :
        GearEnergyTransformer, IGearGenerator, IElectricConsumer, IUpdatableBlockComponent, IBlockStateDetail, IBlockSaveState
    {
        public int TeethCount => _param.TeethCount;
        public bool GenerateIsClockwise => true;

        // RPM は選択モードで固定。ただし実効トルクが0（充足率0=電力0、またはモードのトルクが0）のときは RPM も 0 にする。
        // 実効出力0の generator が固定RPMのまま網の最速起点になり、実際に動ける他の generator を
        // OverRequirePower で停止/方向ロックさせるのを防ぐ（外部監査A: 重大。給電済みでもトルク0設定なら同様に支配を防ぐ）。
        // RPM is fixed by the mode, but drops to 0 when effective torque is 0 (no power, or a torque-0 mode).
        // This stops a zero-output generator from becoming the fastest origin and stalling real generators (audit A: critical; also guards a powered torque-0 mode).
        public RPM GenerateRpm => (_electricFulfillmentRate > 0f && CurrentMode.Torque > 0) ? new RPM((float)CurrentMode.Rpm) : new RPM(0);

        // トルクは電力充足率でドループ。
        // Torque droops by electric fulfillment.
        public Torque GenerateTorque => new Torque((float)CurrentMode.Torque * _electricFulfillmentRate);

        public string SaveKey => "electricToGearGenerator";

        private readonly ElectricToGearGeneratorBlockParam _param;
        private int _selectedIndex;
        private ElectricPower _suppliedPower;
        private float _electricFulfillmentRate;
        private bool _poweredThisTick;

        // 選択中の出力エントリ。index は常に範囲内に保つ。
        // The currently selected output entry; index is always kept in range.
        private OutputModesElement CurrentMode => _param.OutputModes[_selectedIndex];

        public ElectricToGearGeneratorComponent(
            ElectricToGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(null, blockInstanceId, connectorComponent)
        {
            _param = param;
            _selectedIndex = 0;
            _suppliedPower = new ElectricPower(0);
            _electricFulfillmentRate = 0f;
        }

        // セーブ復元用コンストラクタ
        // Constructor used to restore from saved state
        public ElectricToGearGeneratorComponent(
            System.Collections.Generic.Dictionary<string, string> componentStates,
            ElectricToGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            this(param, blockInstanceId, connectorComponent)
        {
            if (componentStates != null && componentStates.TryGetValue(SaveKey, out var raw) && int.TryParse(raw, out var index))
            {
                _selectedIndex = ClampIndex(index);
            }

            #region Internal

            // 保存値が現在の outputModes 範囲外でも安全な index に丸める
            // Clamp a restored index into the current outputModes range
            int ClampIndex(int i)
            {
                if (i < 0) return 0;
                if (i >= _param.OutputModes.Length) return _param.OutputModes.Length - 1;
                return i;
            }

            #endregion
        }

        #region IElectricConsumer

        // 要求電力は選択エントリの requiredPower で固定（負荷にも RPM にも依存しない）
        // RequestEnergy is fixed at the selected entry's requiredPower (independent of load/RPM)
        public ElectricPower RequestEnergy => new ElectricPower((float)CurrentMode.RequiredPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _poweredThisTick = true;
            UpdateFulfillment(power);
        }

        // 供給電力を保存し、現在モードの requiredPower に対する充足率を再計算する。
        // Store supplied power and recompute fulfillment against the current mode's requiredPower.
        private void UpdateFulfillment(ElectricPower power)
        {
            _suppliedPower = power;
            var required = (float)CurrentMode.RequiredPower;
            _electricFulfillmentRate = required > 0f ? Math.Min(power.AsPrimitive() / required, 1f) : 0f;
        }

        #endregion

        // 電力網から切断されると SupplyEnergy が呼ばれなくなる。そのままだと最後の充足率を保持して
        // 切断後もギア出力を続けてしまう。SupplyEnergy が来ないティックで出力を0へ落とすことで、
        // 購読順に依らず最大1〜2 update 以内に必ず0へ収束させる（即時ではない／外部監査#4対応）。
        // When disconnected, SupplyEnergy stops being called. Resetting output on a no-supply tick guarantees
        // convergence to 0 within at most 1-2 updates regardless of subscription order (not instant; audit #4).
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (!_poweredThisTick)
            {
                UpdateFulfillment(new ElectricPower(0));
            }
            _poweredThisTick = false;
        }

        // プロトコルから呼ばれる出力モード切替。範囲内なら適用して true、範囲外は無視して false。
        // Output-mode switch called by the protocol; returns true if applied, false if out-of-range (ignored).
        public bool SetSelectedMode(int index)
        {
            BlockException.CheckDestroy(this);
            if (index < 0 || index >= _param.OutputModes.Length) return false;
            _selectedIndex = index;

            // 直前の供給電力を新モードの requiredPower で再評価する。これをしないと、低消費→高出力モードへ
            // 切替えた直後の1ティックだけ旧充足率で高トルクを無料出力してしまう（外部監査B対応）。
            // Re-evaluate the last supplied power against the new mode's requiredPower; otherwise a low→high
            // switch emits one free high-torque tick at the stale fulfillment (external audit B).
            UpdateFulfillment(_suppliedPower);
            return true;
        }

        public int SelectedIndex => _selectedIndex;

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return _selectedIndex.ToString();
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
                var detail = new ElectricToGearGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
                    _selectedIndex,
                    _electricFulfillmentRate,
                    _suppliedPower);
                var serialized = MessagePackSerializer.Serialize(detail);
                return new BlockStateDetail(ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey, serialized);
            }

            #endregion
        }
    }
}

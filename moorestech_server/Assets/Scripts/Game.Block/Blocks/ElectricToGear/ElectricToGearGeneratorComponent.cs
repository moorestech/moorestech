using System;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.ElectricToGear
{
    public class ElectricToGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IElectricConsumer, IUpdatableBlockComponent, IBlockStateDetail, IBlockSaveState, IBlockStateObservable
    {
        public int TeethCount => _param.TeethCount;
        public bool GenerateIsClockwise => true;
        
        public RPM GenerateRpm => _electricFulfillmentRate > 0f && CurrentMode.Torque > 0 ? new RPM(CurrentMode.Rpm) : new RPM(0);

        // トルクは電力充足率でドループ。
        // Torque droops by electric fulfillment.
        public Torque GenerateTorque => new(CurrentMode.Torque * _electricFulfillmentRate);

        // 出力は電力側のSupplyEnergyで駆動されるため、gear側の毎tick処理は不要
        // Output is driven by the electric side's SupplyEnergy, so no gear-side per-tick processing is needed
        public bool RequiresContinuousTick => false;

        public void ConsumeGeneratorTick(float networkLoadRate)
        {
        }

        public string SaveKey => "electricToGearGenerator";

        // モード選択・電力充足率の変化をクライアントへ通知する状態変化ストリーム
        // State-change stream notifying the client when the mode or electric fulfillment changes
        public new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        private readonly ElectricToGearGeneratorBlockParam _param;
        private readonly Subject<Unit> _onChangeBlockState = new Subject<Unit>();

        // 基底（ギア網）の状態変化を自前の Subject へ転送する購読
        // Subscription forwarding base (gear-network) state changes into our own Subject
        // 注: 基底 Destroy() は非 virtual で破棄フックが無く、FuelGearGenerator 同様 Subject/購読は明示破棄しない（両者ともこの同一インスタンスのフィールドで、外部参照を残さないため自然に解放される）
        // Note: base Destroy() is non-virtual with no dispose hook; like FuelGearGenerator we don't explicitly dispose the Subject/subscription (both ends are fields of this same instance, holding no external reference, so they free naturally)
        private readonly System.IDisposable _baseStateForward;

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

            // 基底のギア状態変化を自前ストリームへ転送（RPM/トルク変化もクライアントへ届ける）
            // Forward base gear-state changes into our stream so RPM/torque changes also reach the client
            _baseStateForward = base.OnChangeBlockState.Subscribe(_ => _onChangeBlockState.OnNext(Unit.Default));
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

        public ElectricPower RequestEnergy => new ElectricPower((float)CurrentMode.RequiredPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _poweredThisTick = true;
            UpdateFulfillment(power);
        }

        private void UpdateFulfillment(ElectricPower power)
        {
            _suppliedPower = power;
            var required = (float)CurrentMode.RequiredPower;
            // 充足率を計算
            // Calculate fulfillment rate
            var newRate = required > 0f ? Math.Min(power.AsPrimitive() / required, 1f) : 0f;

            // 充足率が変化したら出力再配分を自己通知し、クライアントへも状態変化を通知
            // On fulfillment change, self-notify for redistribution and notify the client of the state change
            var changed = Math.Abs(newRate - _electricFulfillmentRate) > 0.0001f;
            _electricFulfillmentRate = newRate;
            if (changed)
            {
                GearNetworkDatastore.NotifyGeneratorOutputChanged(this);
                _onChangeBlockState.OnNext(Unit.Default);
            }
        }

        #endregion

        // 電力network側の未リファクタ都合で残すIUpdatableBlockComponent。電力供給の途絶検知のみでgear動力計算はしない
        // IUpdatableBlockComponent kept due to the unrefactored electric network; it only detects lost electric supply, no gear power calc
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (!_poweredThisTick)
            {
                UpdateFulfillment(new ElectricPower(0));
            }
            _poweredThisTick = false;
        }

        // 出力モード切替
        // Switch output mode
        public bool SetSelectedMode(int index)
        {
            BlockException.CheckDestroy(this);
            if (index < 0 || index >= _param.OutputModes.Length) return false;
            var changed = _selectedIndex != index;
            _selectedIndex = index;

            // 新モードの requiredPower で再評価して充足率を更新
            // Re-evaluate and update fulfillment against the new mode's requiredPower
            UpdateFulfillment(_suppliedPower);

            // モード自体が変わったらクライアントへ状態変化を通知
            // Notify the client of a state change when the mode index itself changes
            if (changed) _onChangeBlockState.OnNext(Unit.Default);
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

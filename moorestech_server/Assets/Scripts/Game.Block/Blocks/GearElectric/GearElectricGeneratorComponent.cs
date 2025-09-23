using System;
using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.GearElectric
{
    /// <summary>
    /// 歯車発電機コンポーネント
    /// 歯車ネットワークから受け取った機械的エネルギー（RPMとトルク）を電力に変換する
    /// </summary>
    public class GearElectricGeneratorComponent :
        IGearEnergyTransformer,
        IElectricGenerator,
        IUpdatableBlockComponent,
        IBlockSaveState,
        IBlockStateDetail
    {
        // パラメータ
        private readonly GearElectricGeneratorBlockParam _param;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly BlockPositionInfo _blockPositionInfo;
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _gearConnector;

        // 状態
        private RPM _inputRpm;
        private Torque _inputTorque;
        private bool _isClockwise;
        private float _energyFulfillmentRate;
        private ElectricPower _currentGeneratedPower;
        private bool _isRocked;

        // イベント
        public event Action<ElectricPower> OnChangeGeneratedPower;
        public event Action<BlockStateDetail[]> OnChangeBlockState;

        // プロパティ
        public BlockInstanceId BlockInstanceId => _blockInstanceId;
        public BlockPositionInfo BlockPositionInfo => _blockPositionInfo;

        // IGearEnergyTransformer
        public bool IsRocked => _isRocked;
        public RPM CurrentRpm => _inputRpm;
        public Torque CurrentTorque => _inputTorque;
        public bool IsCurrentClockwise => _isClockwise;

        // IBlockSaveState
        public string SaveKey => nameof(GearElectricGeneratorComponent);
        public bool IsDestroy { get; private set; } = false;

        public GearElectricGeneratorComponent(
            GearElectricGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo,
            IBlockConnectorComponent<IGearEnergyTransformer> gearConnector)
        {
            _param = param ?? throw new ArgumentNullException(nameof(param));
            _blockInstanceId = blockInstanceId;
            _blockPositionInfo = blockPositionInfo;
            _gearConnector = gearConnector ?? throw new ArgumentNullException(nameof(gearConnector));

            _inputRpm = new RPM(0);
            _inputTorque = new Torque(0);
            _currentGeneratedPower = new ElectricPower(0);
            _energyFulfillmentRate = 0f;
            _isRocked = false;
            _isClockwise = true;
        }

        // セーブデータから復元するコンストラクタ
        public GearElectricGeneratorComponent(
            Dictionary<string, string> componentStates,
            GearElectricGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo,
            IBlockConnectorComponent<IGearEnergyTransformer> gearConnector)
            : this(param, blockInstanceId, blockPositionInfo, gearConnector)
        {
            if (componentStates != null && componentStates.TryGetValue(SaveKey, out var saveData))
            {
                var data = MessagePackSerializer.Deserialize<GearElectricGeneratorSaveData>(
                    MessagePackSerializer.ConvertFromJson(saveData));

                _inputRpm = new RPM(data.InputRpm);
                _inputTorque = new Torque(data.InputTorque);
                _isClockwise = data.IsClockwise;
                _energyFulfillmentRate = data.EnergyFulfillmentRate;
                _currentGeneratedPower = new ElectricPower(data.CurrentGeneratedPower);
                _isRocked = data.IsRocked;
            }
        }

        public void Update()
        {
            UpdateGeneratedPower();
        }

        // IGearEnergyTransformer実装
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // 発電量に応じた負荷トルクを返す
            // 発電機は動力を消費して電力を生成するため、エネルギー充足率に応じた負荷を発生させる
            return new Torque(_param.RequiredTorque * _energyFulfillmentRate);
        }

        public void Rocked()
        {
            _isRocked = true;
            _inputRpm = new RPM(0);
            _inputTorque = new Torque(0);
            UpdateGeneratedPower();
        }

        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            _isRocked = false;
            _inputRpm = rpm;
            _inputTorque = torque;
            _isClockwise = isClockwise;
            UpdateGeneratedPower();
        }

        public List<GearConnect> GetGearConnects()
        {
            var result = new List<GearConnect>();
            foreach (var (target, info) in _gearConnector.ConnectedTargets)
            {
                result.Add(new GearConnect(target,
                    (GearConnectOption)info.SelfOption,
                    (GearConnectOption)info.TargetOption));
            }
            return result;
        }

        // IElectricGenerator実装
        public ElectricPower OutputEnergy()
        {
            return _currentGeneratedPower;
        }

        // 発電量計算ロジック
        private void UpdateGeneratedPower()
        {
            // ゼロチェック
            if (_inputRpm.AsPrimitive() <= 0 || _inputTorque.AsPrimitive() <= 0 || _param.RequiredRpm <= 0 || _param.RequiredTorque <= 0)
            {
                _energyFulfillmentRate = 0f;
                _currentGeneratedPower = new ElectricPower(0);
                OnChangeGeneratedPower?.Invoke(_currentGeneratedPower);
                OnChangeBlockState?.Invoke(GetBlockStateDetails());
                return;
            }

            // エネルギー充足率の計算
            var rpmRate = _inputRpm.AsPrimitive() / _param.RequiredRpm;
            var torqueRate = _inputTorque.AsPrimitive() / _param.RequiredTorque;

            // 充足率は各要素の積
            _energyFulfillmentRate = rpmRate * torqueRate;

            // 100%を超える場合はクリッピング
            if (_energyFulfillmentRate > 1.0f)
            {
                _energyFulfillmentRate = 1.0f;
            }

            // 線形な発電量計算
            var newGeneratedPower = new ElectricPower(
                (int)(_param.MaxGeneratedPower * _energyFulfillmentRate)
            );

            // 発電量が変化した場合はイベント発火
            if (Math.Abs(_currentGeneratedPower.AsPrimitive() - newGeneratedPower.AsPrimitive()) > 0.01f)
            {
                _currentGeneratedPower = newGeneratedPower;
                OnChangeGeneratedPower?.Invoke(_currentGeneratedPower);
                OnChangeBlockState?.Invoke(GetBlockStateDetails());
            }
        }

        // IBlockComponent実装
        public void Destroy()
        {
            IsDestroy = true;
        }

        // IBlockSaveState実装
        public string GetSaveState()
        {
            var saveData = new GearElectricGeneratorSaveData
            {
                InputRpm = _inputRpm.AsPrimitive(),
                InputTorque = _inputTorque.AsPrimitive(),
                IsClockwise = _isClockwise,
                EnergyFulfillmentRate = _energyFulfillmentRate,
                CurrentGeneratedPower = (int)_currentGeneratedPower.AsPrimitive(),
                IsRocked = _isRocked
            };

            return MessagePackSerializer.SerializeToJson(saveData);
        }

        // IBlockStateDetail実装
        public BlockStateDetail[] GetBlockStateDetails()
        {
            var stateDetail = new GearElectricGeneratorBlockStateDetail
            {
                InputRpm = _inputRpm.AsPrimitive(),
                InputTorque = _inputTorque.AsPrimitive(),
                EnergyFulfillmentRate = _energyFulfillmentRate * 100f, // パーセンテージ表示
                GeneratedPower = (int)_currentGeneratedPower.AsPrimitive(),
                IsRocked = _isRocked
            };

            return new[]
            {
                new BlockStateDetail(
                    "GearElectricGeneratorState",
                    MessagePackSerializer.Serialize(stateDetail)
                )
            };
        }

        // セーブデータ構造
        [MessagePackObject]
        public class GearElectricGeneratorSaveData
        {
            [Key(0)] public float InputRpm { get; set; }
            [Key(1)] public float InputTorque { get; set; }
            [Key(2)] public bool IsClockwise { get; set; }
            [Key(3)] public float EnergyFulfillmentRate { get; set; }
            [Key(4)] public int CurrentGeneratedPower { get; set; }
            [Key(5)] public bool IsRocked { get; set; }
        }

        // 状態詳細データ構造
        [MessagePackObject]
        public class GearElectricGeneratorBlockStateDetail
        {
            [Key(0)] public float InputRpm { get; set; }
            [Key(1)] public float InputTorque { get; set; }
            [Key(2)] public float EnergyFulfillmentRate { get; set; }
            [Key(3)] public int GeneratedPower { get; set; }
            [Key(4)] public bool IsRocked { get; set; }
        }
    }
}
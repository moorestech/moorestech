using System;
using System.Collections.Generic;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class FuelGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IUpdatableBlockComponent, IBlockSaveState, IBlockStateObservable
    {
        public string SaveKey => "fuelGearGenerator";
        
        public int TeethCount { get; }
        public RPM GenerateRpm { get; private set; }
        public Torque GenerateTorque { get; private set; }
        public bool GenerateIsClockwise => true;
        public new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        // ギア生成に必要な子コンポーネントとサービスを集約して保持する
        // Hold child components and services required to drive gear generation
        private readonly FuelGearGeneratorFluidComponent _fluidComponent;
        private readonly FuelGearGeneratorFuelService _fuelService;
        private readonly FuelGearGeneratorStateService _stateService;
        private readonly Subject<Unit> _onChangeBlockState;

        // コンストラクタで依存コンポーネントを受け取り、初期状態を整える
        // Accept dependent components via constructor and set up the initial generator state
        public FuelGearGeneratorComponent(
            FuelGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            FuelGearGeneratorItemComponent itemComponent,
            FuelGearGeneratorFluidComponent fluidComponent)
            : base(new Torque(0), blockInstanceId, connectorComponent)
        {
            _fluidComponent = fluidComponent;
            _fuelService = new FuelGearGeneratorFuelService(param, itemComponent.InventoryService, fluidComponent);
            _stateService = new FuelGearGeneratorStateService(param, _fuelService, fluidComponent);
            _onChangeBlockState = new Subject<Unit>();

            TeethCount = param.TeethCount;
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
        }

        // セーブデータから復元する際に呼ばれる補助コンストラクタ
        // Auxiliary constructor used for restoring the component from saved state
        public FuelGearGeneratorComponent(
            Dictionary<string, string> componentStates,
            FuelGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            FuelGearGeneratorItemComponent itemComponent,
            FuelGearGeneratorFluidComponent fluidComponent)
            : this(param, blockInstanceId, connectorComponent, itemComponent, fluidComponent)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saveData = JsonUtility.FromJson<FuelGearGeneratorSaveData>(raw);

            _fuelService.Restore(saveData);
            _stateService.Restore(saveData);
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
        }

        // フレーム更新で燃料と状態を処理し、出力がある限り観測者へ通知する
        // Process fuel and state each frame, notifying observers while power is produced
        public override void Update()
        {
            BlockException.CheckDestroy(this);
            if (IsDestroy) return;

            var network = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
            var operatingRate = network.CurrentGearNetworkInfo.OperatingRate;
            
            var changed = _stateService.TryUpdate(operatingRate, out var newRpm, out var newTorque);
            GenerateRpm = newRpm;
            GenerateTorque = newTorque;

            if (changed || newRpm.AsPrimitive() > 0f)
            {
                _onChangeBlockState.OnNext(Unit.Default);
            }
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var saveData = new FuelGearGeneratorSaveData(_stateService, _fuelService);
            return JsonUtility.ToJson(saveData);
        }

        public new BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            
            var network = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
            var fuelGearGeneratorDetail = CreateFuelGearGeneratorStateDetail();
            var powerGeneratorDetail = CreatePowerGeneratorStateDetail();
            
            const int addCount = 2;
            var baseDetails = base.GetBlockStateDetails();
            var result = new BlockStateDetail[baseDetails.Length + addCount];
            result[0] = fuelGearGeneratorDetail;
            result[1] = powerGeneratorDetail;
            
            Array.Copy(baseDetails, 0, result, addCount, baseDetails.Length);
            return result;
            
            #region Internal
            
            BlockStateDetail CreateFuelGearGeneratorStateDetail()
            {
                var gearGenerator = new FuelGearGeneratorBlockStateDetail(_stateService, _fluidComponent, network.CurrentGearNetworkInfo, GenerateIsClockwise);
                return new BlockStateDetail(FuelGearGeneratorBlockStateDetail.FuelGearGeneratorBlockStateDetailKey, MessagePackSerializer.Serialize(gearGenerator));
            }
            
            BlockStateDetail CreatePowerGeneratorStateDetail()
            {
                var operatingRate = network.CurrentGearNetworkInfo.OperatingRate;
                var powerGeneratorStateDetail = new PowerGeneratorStateDetail(_fuelService, operatingRate);
                return new BlockStateDetail(PowerGeneratorStateDetail.StateDetailKey, MessagePackSerializer.Serialize(powerGeneratorStateDetail));
            }
            
            #endregion
        }
    }
}

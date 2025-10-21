using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    public class SteamGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IUpdatableBlockComponent, IBlockSaveState, IBlockStateObservable
    {
        public string SaveKey => "steamGearGenerator";
        
        public int TeethCount { get; }
        public RPM GenerateRpm { get; private set; }
        public Torque GenerateTorque { get; private set; }
        public bool GenerateIsClockwise => true;
        public new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        // ギア生成に必要な子コンポーネントとサービスを集約して保持する
        // Hold child components and services required to drive gear generation
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;
        private readonly SteamGearGeneratorFuelService _fuelService;
        private readonly SteamGearGeneratorStateService _stateService;
        private readonly Subject<Unit> _onChangeBlockState;

        // コンストラクタで依存コンポーネントを受け取り、初期状態を整える
        // Accept dependent components via constructor and set up the initial generator state
        public SteamGearGeneratorComponent(
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            SteamGearGeneratorItemComponent itemComponent,
            SteamGearGeneratorFluidComponent fluidComponent)
            : base(new Torque(0), blockInstanceId, connectorComponent)
        {
            _fluidComponent = fluidComponent;
            _fuelService = new SteamGearGeneratorFuelService(param, itemComponent.InventoryService, fluidComponent);
            _stateService = new SteamGearGeneratorStateService(param, _fuelService, fluidComponent);
            _onChangeBlockState = new Subject<Unit>();

            TeethCount = param.TeethCount;
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
        }

        // セーブデータから復元する際に呼ばれる補助コンストラクタ
        // Auxiliary constructor used for restoring the component from saved state
        public SteamGearGeneratorComponent(
            Dictionary<string, string> componentStates,
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            SteamGearGeneratorItemComponent itemComponent,
            SteamGearGeneratorFluidComponent fluidComponent)
            : this(param, blockInstanceId, connectorComponent, itemComponent, fluidComponent)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saveData = JsonConvert.DeserializeObject<SteamGearGeneratorSaveData>(raw);
            if (saveData == null) return;

            var fuelType = Enum.TryParse(saveData.ActiveFuelType, out SteamGearGeneratorFuelService.FuelType parsed)
                ? parsed
                : SteamGearGeneratorFuelService.FuelType.None;

            _fuelService.Restore(new SteamGearGeneratorFuelService.FuelState
            {
                ActiveFuelType = fuelType,
                CurrentFuelItemGuid = saveData.CurrentFuelItemGuid,
                CurrentFuelFluidGuid = saveData.CurrentFuelFluidGuid,
                RemainingFuelTime = saveData.RemainingFuelTime
            });

            var snapshot = new SteamGearGeneratorStateService.StateSnapshot
            {
                State = saveData.CurrentState,
                StateElapsedTime = saveData.StateElapsedTime,
                SteamConsumptionRate = saveData.SteamConsumptionRate,
                RateAtDecelerationStart = saveData.RateAtDecelerationStart
            };

            _stateService.Restore(snapshot);
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
        }

        // フレーム更新で燃料と状態を処理し、出力がある限り観測者へ通知する
        // Process fuel and state each frame, notifying observers while power is produced
        public void Update()
        {
            BlockException.CheckDestroy(this);

            var changed = _stateService.TryUpdate(out var newRpm, out var newTorque);
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
            var saveData = new SteamGearGeneratorSaveData(_stateService, _fuelService);
            return JsonConvert.SerializeObject(saveData);
        }

        public new BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var network = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
            var detail = new SteamGearGeneratorBlockStateDetail(_stateService, _fluidComponent, network.CurrentGearNetworkInfo, GenerateIsClockwise);
            var serialised = MessagePackSerializer.Serialize(detail);
            var baseDetails = base.GetBlockStateDetails();

            var result = new BlockStateDetail[baseDetails.Length + 1];
            result[0] = new BlockStateDetail(SteamGearGeneratorBlockStateDetail.SteamGearGeneratorBlockStateDetailKey, serialised);
            Array.Copy(baseDetails, 0, result, 1, baseDetails.Length);
            return result;
        }
    }
}

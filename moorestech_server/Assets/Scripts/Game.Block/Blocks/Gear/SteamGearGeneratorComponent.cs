using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Gear
{
    public class SteamGearGeneratorComponent : GearEnergyTransformer, IGearGenerator, IUpdatableBlockComponent, IBlockSaveState, IBlockStateObservable, IBlockInventory, IOpenableInventory
    {
        public int TeethCount { get; }
        public RPM GenerateRpm { get; private set; }
        public Torque GenerateTorque { get; private set; }
        public bool GenerateIsClockwise => true;
        public new IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        public IReadOnlyList<IItemStack> InventoryItems => _inventoryService.InventoryItems;
        public bool GenerateIsActive => _steamConsumptionRate > 0f;
        public bool GenerateIsReady => _currentState is GeneratorState.Accelerating or GeneratorState.Running;

        // 生成に必要なパラメータ・サービスと現在の状態管理フィールド群
        // Core parameters, services, and state trackers required for power generation
        private readonly SteamGearGeneratorBlockParam _param;
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;
        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly SteamGearGeneratorFuelService _fuelService;
        private readonly Subject<Unit> _onChangeBlockState;

        private GeneratorState _currentState = GeneratorState.Idle;
        private float _stateElapsedTime;
        private float _steamConsumptionRate;
        private float _rateAtDecelerationStart;

        private enum GeneratorState
        {
            Idle,
            Accelerating,
            Running,
            Decelerating
        }

        // コンストラクタで依存関係を受け取り初期状態をセットアップする
        // Accept dependencies during construction and configure the initial generator state
        public SteamGearGeneratorComponent(
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            SteamGearGeneratorFluidComponent fluidComponent)
            : base(new Torque(0), blockInstanceId, connectorComponent)
        {
            _param = param;
            _fluidComponent = fluidComponent;
            var slotCount = Math.Max(0, param.FuelItemSlotCount);
            _inventoryService = new OpenableInventoryItemDataStoreService(InvokeInventoryUpdate, ServerContext.ItemStackFactory, slotCount);
            _fuelService = new SteamGearGeneratorFuelService(param, _inventoryService, fluidComponent);
            TeethCount = param.TeethCount;
            GenerateRpm = new RPM(0);
            GenerateTorque = new Torque(0);
            _stateElapsedTime = 0f;
            _steamConsumptionRate = 0f;
            _rateAtDecelerationStart = 0f;
            _onChangeBlockState = new Subject<Unit>();
        }

        public SteamGearGeneratorComponent(
            Dictionary<string, string> componentStates,
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            SteamGearGeneratorFluidComponent fluidComponent)
            : this(param, blockInstanceId, connectorComponent, fluidComponent)
        {
            if (!componentStates.TryGetValue(SaveKey, out var saveState)) return;

            var saveData = JsonConvert.DeserializeObject<SteamGearGeneratorSaveData>(saveState);
            if (saveData == null) return;

            if (Enum.TryParse(saveData.CurrentState, out GeneratorState parsedState))
            {
                _currentState = parsedState;
            }

            _stateElapsedTime = saveData.StateElapsedTime;
            _steamConsumptionRate = saveData.SteamConsumptionRate;
            _rateAtDecelerationStart = saveData.RateAtDecelerationStart;
            var restoredFuelType = Enum.TryParse(saveData.ActiveFuelType, out SteamGearGeneratorFuelService.FuelType parsedFuelType) ? parsedFuelType : SteamGearGeneratorFuelService.FuelType.None;
            RestoreInventory(saveData.Items);
            _fuelService.Restore(new SteamGearGeneratorFuelService.FuelState
            {
                ActiveFuelType = restoredFuelType,
                CurrentFuelItemGuid = saveData.CurrentFuelItemGuid,
                CurrentFuelFluidGuid = saveData.CurrentFuelFluidGuid,
                RemainingFuelTime = saveData.RemainingFuelTime
            });

            UpdateOutput();

            #region Internal

            void RestoreInventory(List<ItemStackSaveJsonObject> items)
            {
                if (items == null) return;

                var slotSize = _inventoryService.GetSlotSize();
                for (var i = 0; i < Math.Min(slotSize, items.Count); i++)
                {
                    var stack = items[i]?.ToItemStack();
                    if (stack == null) continue;
                    _inventoryService.SetItemWithoutEvent(i, stack);
                }
            }

            #endregion
        }

        // 毎フレーム呼ばれ、燃料処理と出力更新をまとめて行う
        // Called every frame to process fuel consumption and refresh output values
        public void Update()
        {
            BlockException.CheckDestroy(this);
            UpdateState();
            UpdateOutput();
        }

        // 燃料・流体状況を元に現在の動作状態を遷移させる
        // Transition the generator state based on the available item and fluid fuel
        private void UpdateState()
        {
            _fuelService.Update();
            var allowFluidFuel = !_fluidComponent.IsPipeDisconnected;
            var hasFuel = _fuelService.HasAvailableFuel(allowFluidFuel);
            var shouldForceDeceleration = _fuelService.IsUsingFluidFuel && !allowFluidFuel;
            _stateElapsedTime += (float)GameUpdater.UpdateSecondTime;

            switch (_currentState)
            {
                case GeneratorState.Idle:
                    if (hasFuel && _fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Accelerating);
                    }
                    else
                    {
                        _steamConsumptionRate = 0f;
                    }
                    break;

                case GeneratorState.Accelerating:
                    if (shouldForceDeceleration)
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    else if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    else if (_steamConsumptionRate >= 1f)
                    {
                        TransitionToState(GeneratorState.Running);
                    }
                    break;

                case GeneratorState.Running:
                    if (shouldForceDeceleration)
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    else if (!_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Decelerating);
                    }
                    break;

                case GeneratorState.Decelerating:
                    if (_steamConsumptionRate <= 0f)
                    {
                        TransitionToState(GeneratorState.Idle);
                    }
                    else if (_fuelService.TryEnsureFuel(allowFluidFuel))
                    {
                        TransitionToState(GeneratorState.Accelerating);
                    }
                    break;
            }

            #region Internal

            void TransitionToState(GeneratorState newState)
            {
                if (_currentState == newState) return;

                if (newState == GeneratorState.Decelerating)
                {
                    _rateAtDecelerationStart = _steamConsumptionRate;
                }

                _currentState = newState;
                _stateElapsedTime = 0f;
                _onChangeBlockState.OnNext(Unit.Default);
            }

            #endregion
        }

        private void UpdateOutput()
        {
            switch (_currentState)
            {
                case GeneratorState.Idle:
                    _steamConsumptionRate = 0f;
                    break;

                case GeneratorState.Accelerating:
                    var accelerationProgress = Mathf.Clamp01(_stateElapsedTime / _param.TimeToMax);
                    _steamConsumptionRate = ApplyEasing(accelerationProgress, _param.TimeToMaxEasing);
                    break;

                case GeneratorState.Running:
                    _steamConsumptionRate = 1f;
                    break;

                case GeneratorState.Decelerating:
                    var decelProgress = Mathf.Clamp01(_stateElapsedTime / _param.TimeToMax);
                    var eased = ApplyEasing(decelProgress, _param.TimeToMaxEasing);
                    _steamConsumptionRate = _rateAtDecelerationStart * (1f - eased);
                    break;
            }

            var newRpm = new RPM(_param.GenerateMaxRpm * _steamConsumptionRate);
            var newTorque = new Torque(_param.GenerateMaxTorque * _steamConsumptionRate);

            if (Math.Abs(GenerateRpm.AsPrimitive() - newRpm.AsPrimitive()) > 0.001f ||
                Math.Abs(GenerateTorque.AsPrimitive() - newTorque.AsPrimitive()) > 0.001f)
            {
                GenerateRpm = newRpm;
                GenerateTorque = newTorque;
                _onChangeBlockState.OnNext(Unit.Default);
            }
        }

        // 加速・減速の補間を指示されたイージング種別で計算する
        // Calculate acceleration and deceleration curves using the requested easing type
        private float ApplyEasing(float t, string easingType)
        {
            switch (easingType)
            {
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.Linear:
                    return t;
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInSine:
                    return 1 - Mathf.Cos((t * Mathf.PI) / 2f);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutSine:
                    return Mathf.Sin((t * Mathf.PI) / 2f);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCubic:
                    return t * t * t;
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCubic:
                    return 1 - Mathf.Pow(1 - t, 3);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInQuint:
                    return t * t * t * t * t;
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutQuint:
                    return 1 - Mathf.Pow(1 - t, 5);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseInCirc:
                    return 1 - Mathf.Sqrt(1 - t * t);
                case SteamGearGeneratorBlockParam.TimeToMaxEasingConst.EaseOutCirc:
                    return Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));
                default:
                    return t;
            }
        }

        public string SaveKey => "steamGearGenerator";

        public string GetSaveState()
        {
            var saveData = new SteamGearGeneratorSaveData
            {
                CurrentState = _currentState.ToString(),
                StateElapsedTime = _stateElapsedTime,
                SteamConsumptionRate = _steamConsumptionRate,
                RateAtDecelerationStart = _rateAtDecelerationStart,
                Items = new List<ItemStackSaveJsonObject>()
            };

            var fuelState = _fuelService.CreateSnapshot();
            saveData.ActiveFuelType = fuelState.ActiveFuelType.ToString();
            saveData.RemainingFuelTime = fuelState.RemainingFuelTime;
            saveData.CurrentFuelItemGuidStr = fuelState.CurrentFuelItemGuid?.ToString();
            saveData.CurrentFuelFluidGuidStr = fuelState.CurrentFuelFluidGuid?.ToString();

            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < slotSize; i++)
            {
                saveData.Items.Add(new ItemStackSaveJsonObject(_inventoryService.GetItem(i)));
            }

            return JsonConvert.SerializeObject(saveData);
        }

        private class SteamGearGeneratorSaveData
        {
            public string CurrentState { get; set; }
            public float StateElapsedTime { get; set; }
            public float SteamConsumptionRate { get; set; }
            public float RateAtDecelerationStart { get; set; }
            public List<ItemStackSaveJsonObject> Items { get; set; }
            public string ActiveFuelType { get; set; }
            public double RemainingFuelTime { get; set; }
            public string CurrentFuelItemGuidStr { get; set; }
            public string CurrentFuelFluidGuidStr { get; set; }

            [JsonIgnore]
            public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

            [JsonIgnore]
            public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;
        }

        public new BlockStateDetail[] GetBlockStateDetails()
        {
            var steamGearDetail = GetSteamGearDetail();
            var baseDetails = base.GetBlockStateDetails();

            var resultDetails = new BlockStateDetail[baseDetails.Length + 1];
            resultDetails[0] = steamGearDetail;
            Array.Copy(baseDetails, 0, resultDetails, 1, baseDetails.Length);

            return resultDetails;

            #region Internal

            BlockStateDetail GetSteamGearDetail()
            {
                var network = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
                var gearNetworkInfo = network.CurrentGearNetworkInfo;

                var stateDetail = new SteamGearGeneratorBlockStateDetail(
                    _currentState.ToString(),
                    GenerateRpm,
                    GenerateTorque,
                    GenerateIsClockwise,
                    _steamConsumptionRate,
                    _fluidComponent.SteamTank,
                    gearNetworkInfo
                );

                var serializedState = MessagePackSerializer.Serialize(stateDetail);
                return new BlockStateDetail(SteamGearGeneratorBlockStateDetail.SteamGearGeneratorBlockStateDetailKey, serializedState);
            }

            #endregion
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemStack);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertionCheck(itemStacks);
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            _inventoryService.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.CreateCopiedItems();
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            _inventoryService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemStacks);
        }

        private void InvokeInventoryUpdate(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            var updateEvent = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack);
            updateEvent.OnInventoryUpdateInvoke(properties);
        }
    }
}

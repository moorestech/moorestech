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
        public bool GenerateIsActive => _stateService.SteamConsumptionRate > 0f;
        public bool GenerateIsReady => _stateService.IsReady;

        // ギア発電機の依存オブジェクトと補助サービス参照を保持する
        // Hold references to dependent objects and helper services for the gear generator
        private readonly SteamGearGeneratorBlockParam _param;
        private readonly SteamGearGeneratorFluidComponent _fluidComponent;
        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly SteamGearGeneratorFuelService _fuelService;
        private readonly SteamGearGeneratorStateService _stateService;
        private readonly Subject<Unit> _onChangeBlockState;

        // コンストラクタで依存関係を受け取り初期状態を整える
        // Accept constructor dependencies and configure the initial generator state
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
            _stateService = new SteamGearGeneratorStateService(param, _fuelService, fluidComponent);
            _onChangeBlockState = new Subject<Unit>();

            TeethCount = param.TeethCount;
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
        }

        // セーブデータからインスタンスを復元するためのコンストラクタ
        // Constructor used to restore an instance from saved component data
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

            var restoredFuelType = Enum.TryParse(saveData.ActiveFuelType, out SteamGearGeneratorFuelService.FuelType parsedFuelType)
                ? parsedFuelType
                : SteamGearGeneratorFuelService.FuelType.None;

            // 保存されていたインベントリと燃料状態を順に復元する
            // Restore the previously saved inventory items and fuel state in sequence
            RestoreInventory(saveData.Items);
            _fuelService.Restore(new SteamGearGeneratorFuelService.FuelState
            {
                ActiveFuelType = restoredFuelType,
                CurrentFuelItemGuid = saveData.CurrentFuelItemGuid,
                CurrentFuelFluidGuid = saveData.CurrentFuelFluidGuid,
                RemainingFuelTime = saveData.RemainingFuelTime
            });

            var stateSnapshot = new SteamGearGeneratorStateService.StateSnapshot
            {
                State = saveData.CurrentState,
                StateElapsedTime = saveData.StateElapsedTime,
                SteamConsumptionRate = saveData.SteamConsumptionRate,
                RateAtDecelerationStart = saveData.RateAtDecelerationStart
            };

            _stateService.Restore(stateSnapshot);
            GenerateRpm = _stateService.CurrentGeneratedRpm;
            GenerateTorque = _stateService.CurrentGeneratedTorque;
        }

        // フレーム更新で燃料と状態を処理し、出力が動いている間は常に通知する
        // Process fuel and state each frame while signalling observers whenever power is being produced
        public void Update()
        {
            BlockException.CheckDestroy(this);

            var hasChanges = _stateService.TryUpdate(out var newRpm, out var newTorque);
            GenerateRpm = newRpm;
            GenerateTorque = newTorque;

            // 出力変化または稼働中は観測者へ通知を届ける
            // Notify observers when the output changes or remains active
            var shouldNotify = hasChanges || newRpm.AsPrimitive() > 0f;
            if (shouldNotify)
            {
                _onChangeBlockState.OnNext(Unit.Default);
            }
        }

        // セーブ識別子と現在の状態をシリアライズする
        // Provide the save identifier and serialise the current state
        public string SaveKey => "steamGearGenerator";

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var saveData = new SteamGearGeneratorSaveData(_stateService, _fuelService, _inventoryService);
            return JsonConvert.SerializeObject(saveData);
        }

        // ブロック詳細情報を組み立てて観測用データとして返す
        // Assemble block detail information for observational inspection
        public new BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            var network = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
            var detail = new SteamGearGeneratorBlockStateDetail(_stateService, _fluidComponent, network.CurrentGearNetworkInfo, GenerateIsClockwise);
            var serialized = MessagePackSerializer.Serialize(detail);
            var baseDetails = base.GetBlockStateDetails();

            var resultDetails = new BlockStateDetail[baseDetails.Length + 1];
            resultDetails[0] = new BlockStateDetail(SteamGearGeneratorBlockStateDetail.SteamGearGeneratorBlockStateDetailKey, serialized);
            Array.Copy(baseDetails, 0, resultDetails, 1, baseDetails.Length);
            return resultDetails;
        }
        // IBlockInventory と IOpenableInventory をサービスへ委譲するラッパーメソッド
        // Wrapper methods that delegate inventory operations to the shared service
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemStack);
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.GetItem(slot);
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.GetSlotSize();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertionCheck(itemStacks);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            _inventoryService.SetItem(slot, itemStack);
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

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.CreateCopiedItems();
        }

        // インベントリ更新時にクライアントへ通知イベントを発火させる
        // Emit an update event to notify clients whenever the inventory changes
        private void InvokeInventoryUpdate(int slot, IItemStack itemStack)
        {
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack);
            blockInventoryUpdate.OnInventoryUpdateInvoke(properties);
        }

        // セーブデータのアイテムリストを復元サービスへ反映させる
        // Restore saved inventory items back into the service-managed slots
        private void RestoreInventory(List<ItemStackSaveJsonObject> items)
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
    }
}

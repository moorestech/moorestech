using System.Collections.Generic;
using System.Linq;
using Game.Orbit;
using System.Collections.ObjectModel;
using Core.Inventory;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.Service;
using Game.Block.Interface.Event;
using UniRx;
using Core.Item.Interface;
using Core.Master;
using System;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Game.Block.Interface.BlockException;
namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点のスロット。0は運ぶ衛星の入口、1以降は成果の出口。出口だけを隣のベルトへ押し出す
    // The base's slots: slot 0 takes satellites to carry, the rest hold results; only the outlets are pushed to adjacent belts
    public class SpaceElevatorBaseInventoryComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent, ISortExcludedSlots
    {
        public const int SatelliteSlot = 0;
        public const int FirstOutputSlot = 1;
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        public IReadOnlyCollection<int> SortExcludedSlots => Enumerable.Range(0, _slotCount).ToArray();
        public string SaveKey { get; } = typeof(SpaceElevatorBaseInventoryComponent).FullName;
        internal IObservable<Unit> OnInventoryChanged => _inventoryChanged;
        private readonly Subject<Unit> _inventoryChanged = new();
        private readonly IOrbitalArrivalService _arrivalService;
        private readonly int[] _outputSlots;
        private readonly int _slotCount;
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly HashSet<ItemId> _carriedPayloadIds = new();
        public SpaceElevatorBaseInventoryComponent(BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter, IOrbitalArrivalService arrivalService)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryInserter = blockInventoryInserter;
            _arrivalService = arrivalService;
            _slotCount = FirstOutputSlot + param.OutputSlotCount;
            _outputSlots = Enumerable.Range(FirstOutputSlot, param.OutputSlotCount).ToArray();
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, _slotCount);
            foreach (var payloadGuid in param.CarriedPayloadItemGuids) _carriedPayloadIds.Add(MasterHolder.ItemMaster.GetItemId(payloadGuid));
            #region Internal
            void InvokeEvent(int slot, IItemStack itemStack)
            {
                _inventoryChanged.OnNext(Unit.Default);
                var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
                blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
            }
            #endregion
        }
        public SpaceElevatorBaseInventoryComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter, IOrbitalArrivalService arrivalService) :
            this(blockInstanceId, param, blockInventoryInserter, arrivalService)
        {
            // ロード時はまだワールドに登録されていないのでイベントを出さない
            // Loading does not fire events because the block is not registered in the world yet
            var itemJsons = BlockComponentStateReader.Read<List<ItemStackSaveJsonObject>>(componentStates, SaveKey);
            SpaceElevatorInventoryRestorer.Restore(itemJsons, _itemDataStoreService, _slotCount, blockInstanceId);
        }
        internal bool IsCarriedPayload(ItemId itemId) { return _carriedPayloadIds.Contains(itemId); }
        // 入口の中身を見る。運べないアイテムはマスタを変えた後のロードでだけ残る
        // Peek at the inlet; a non-carried item only remains after loading with a changed master
        internal SatelliteInletState PeekSatellite(out ItemId satelliteId)
        {
            CheckDestroy(this);
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            satelliteId = stack.Id;
            if (stack.Count == 0) return SatelliteInletState.Empty;
            return IsCarriedPayload(stack.Id) ? SatelliteInletState.Carriable : SatelliteInletState.NotCarried;
        }
        // 全量が戻せるときだけ入口へ確定する
        // Commit to the inlet only when the entire satellite stack fits
        internal bool TryReturnSatelliteWithoutEvent(IItemStack satellite)
        {
            var result = _itemDataStoreService.GetItem(SatelliteSlot).AddItem(satellite);
            if (0 < result.RemainderItemStack.Count)
            {
                Debug.LogError($"[SpaceElevatorBase] 入口に衛星を全量戻せないためロードを中断します item:{satellite.Id} count:{satellite.Count}");
                return false;
            }
            _itemDataStoreService.SetItemWithoutEvent(SatelliteSlot, result.ProcessResultItemStack);
            return true;
        }
        // 収納確認から取り出しまでを在庫の所有者が完結する
        // The inventory owner checks capacity and takes the satellite together
        internal SatelliteTakeResult TryTakeSatelliteForClimb(out IItemStack satellite, out ItemId satelliteId)
        {
            CheckDestroy(this);
            satellite = ServerContext.ItemStackFactory.CreatEmpty();
            var inlet = PeekSatellite(out satelliteId);
            // silent-ok: 通常の入口待機 / 呼出側が理由を状態詳細と初回ログへ反映する
            if (inlet != SatelliteInletState.Carriable) return inlet == SatelliteInletState.Empty ? SatelliteTakeResult.Empty : SatelliteTakeResult.NotCarried;
            // silent-ok: 成果の収納待機 / 呼出側が出口待ちをUIへ配信する
            if (!InventoryInsertItem.TryFillAllPreferringExisting(_arrivalService.PreviewArrivalOutputs(satelliteId, 1), CopyOutlets())) return SatelliteTakeResult.OutputBlocked;
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            _itemDataStoreService.SetItem(SatelliteSlot, stack.SubItem(1));
            satellite = ServerContext.ItemStackFactory.Create(stack.Id, 1);
            return SatelliteTakeResult.Taken;
        }
        // 一度だけ模擬し、到着成功後に同じ配置を確定する
        // Simulate once and commit that placement after arrival succeeds
        internal bool TryCompleteArrival(ItemId satelliteId)
        {
            CheckDestroy(this);
            var outputs = _arrivalService.PreviewArrivalOutputs(satelliteId, 1);
            var simulated = CopyOutlets();
            // silent-ok: 容量不足では到着させない / 昇降機が出口待ちをUIと初回ログへ反映する
            if (!InventoryInsertItem.TryFillAllPreferringExisting(outputs, simulated)) return false;
            _arrivalService.Arrive(satelliteId, 1);
            for (var i = 0; i < simulated.Count; i++)
            {
                var current = _itemDataStoreService.GetItem(FirstOutputSlot + i);
                // silent-ok: 不変スロットの通知を抑える / 同じ在庫値は既にUIへ通知済み
                if (simulated[i].Id == current.Id && simulated[i].Count == current.Count) continue;
                _itemDataStoreService.SetItem(FirstOutputSlot + i, simulated[i]);
            }
            return true;
        }
        public void Update()
        {
            CheckDestroy(this);
            BlockOutputTransfer.Transfer(_itemDataStoreService, _outputSlots, _blockInventoryInserter);
        }
        // ベルト・プレイヤーからは運ぶ積荷だけを入口へ受ける
        // Belts and players may only put carried payloads into the inlet
        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);
            // silent-ok: 積荷以外はベルト上に残す通常の受け入れ拒否 / プレイヤーはベルトの詰まりと拠点UIの入口で気づく
            if (!IsCarriedPayload(itemStack.Id)) return itemStack;
            var result = _itemDataStoreService.GetItem(SatelliteSlot).AddItem(itemStack);
            _itemDataStoreService.SetItem(SatelliteSlot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            var slot = _itemDataStoreService.GetItem(SatelliteSlot);
            foreach (var itemStack in itemStacks)
            {
                // silent-ok: 搬送の事前確認は在庫を変えない / 呼び出し側が返値で詰まりを認識する
                if (!IsCarriedPayload(itemStack.Id)) return false;
                var result = slot.AddItem(itemStack);
                // silent-ok: 満杯の通常拒否 / 呼び出し側と入口UIに残数が届く
                if (0 < result.RemainderItemStack.Count) return false;
                slot = result.ProcessResultItemStack;
            }
            return true;
        }
        // 入口は積荷だけ、出口は取り出し（空にする）だけを許す
        // The inlet accepts only payloads and the outlets only taking out (emptying)
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            if (itemStack.Count == 0) return true;
            return slot == SatelliteSlot && IsCarriedPayload(itemStack.Id);
        }
        public object GetSaveState()
        {
            CheckDestroy(this);
            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems) itemJson.Add(new ItemStackSaveJsonObject(item));
            return itemJson;
        }

        private List<IItemStack> CopyOutlets()
        {
            var outlets = new List<IItemStack>();
            for (var slot = FirstOutputSlot; slot < _slotCount; slot++) outlets.Add(_itemDataStoreService.GetItem(slot));
            return outlets;
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { return InsertItem(itemStack); }
        public IItemStack InsertItem(ItemId itemId, int count) { return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count)); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var remainders = new List<IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                var remainder = InsertItem(itemStack);
                if (0 < remainder.Count) remainders.Add(remainder);
            }
            return remainders;
        }
        public void SetItem(int slot, IItemStack itemStack) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemStack); }
        public void SetItem(int slot, ItemId itemId, int count) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemId, count); }
        public IItemStack GetItem(int slot) { CheckDestroy(this); return _itemDataStoreService.GetItem(slot); }
        public int GetSlotSize() { CheckDestroy(this); return _itemDataStoreService.GetSlotSize(); }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { CheckDestroy(this); return _itemDataStoreService.CreateCopiedItems(); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemId, count); }
        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; _inventoryChanged.Dispose(); }
    }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using System;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Game.Block.Interface.BlockException;
namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点のスロット。0は運ぶ衛星の入口、1以降は成果の出口。出口だけを隣のベルトへ押し出す
    // The base's slots: slot 0 takes satellites to carry, the rest hold results; only the outlets are pushed to adjacent belts
    public class SpaceElevatorBaseInventoryComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent
    {
        public const int SatelliteSlot = 0;
        public const int FirstOutputSlot = 1;
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public BlockInstanceId BlockInstanceId { get; }
        public string SaveKey { get; } = typeof(SpaceElevatorBaseInventoryComponent).FullName;
        private readonly int _slotCount;
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly HashSet<ItemId> _carriedPayloadIds = new();
        public SpaceElevatorBaseInventoryComponent(BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryInserter = blockInventoryInserter;
            _slotCount = FirstOutputSlot + param.OutputSlotCount;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, _slotCount);
            foreach (var payloadGuid in param.CarriedPayloadItemGuids) _carriedPayloadIds.Add(MasterHolder.ItemMaster.GetItemId(payloadGuid));
        }

        public SpaceElevatorBaseInventoryComponent(Dictionary<string, object> componentStates, BlockInstanceId blockInstanceId, SpaceElevatorBaseBlockParam param, IBlockInventoryInserter blockInventoryInserter) :
            this(blockInstanceId, param, blockInventoryInserter)
        {
            // ロード時はまだワールドに登録されていないのでイベントを出さない
            // Loading does not fire events because the block is not registered in the world yet
            var itemJsons = BlockComponentStateReader.Read<List<ItemStackSaveJsonObject>>(componentStates, SaveKey);
            for (var i = 0; i < _slotCount && i < itemJsons.Count; i++) _itemDataStoreService.SetItemWithoutEvent(i, itemJsons[i].ToItemStack());
            // マスタで出口を減らすと超えた分は捨てる。黙って捨てずログに残す
            // Reducing the outlets in the master drops the overflow; log it instead of dropping silently
            if (_slotCount < itemJsons.Count) Debug.LogError($"[SpaceElevatorBase] セーブのスロット数 {itemJsons.Count} がマスタの {_slotCount} を超えるため、超えた分を捨てます BlockInstanceId:{blockInstanceId}");
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
        // ロードで取り消した昇降中の衛星を入口へ戻す。入らない分はエラーログを出して失う
        // Return a satellite whose climb was cancelled on load to the inlet; anything that does not fit is logged and lost
        internal void ReturnSatelliteWithoutEvent(IItemStack satellite)
        {
            var result = _itemDataStoreService.GetItem(SatelliteSlot).AddItem(satellite);
            _itemDataStoreService.SetItemWithoutEvent(SatelliteSlot, result.ProcessResultItemStack);
            if (result.RemainderItemStack.Count > 0) Debug.LogError($"[SpaceElevatorBase] 入口が埋まっていて衛星を戻せず失いました item:{satellite.Id} count:{result.RemainderItemStack.Count}");
        }
        // 入口から衛星を1つ取り出す。同じtickのPeekSatelliteがCarriableのときだけ呼ぶ
        // Take one satellite from the inlet; call only after PeekSatellite returned Carriable in the same tick
        internal IItemStack TakeOneSatellite()
        {
            CheckDestroy(this);
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            _itemDataStoreService.SetItem(SatelliteSlot, stack.SubItem(1));
            return ServerContext.ItemStackFactory.Create(stack.Id, 1);
        }
        // 成果が出口へ入りきるかを、出口の複製に積んで確かめる
        // Check whether the results fit by stacking them onto a copy of the outlets
        internal bool CanStoreOutputs(IReadOnlyList<IItemStack> outputs)
        {
            CheckDestroy(this);
            return SpaceElevatorOutputSimulation.TryStack(CopyOutlets(), outputs, out _);
        }
        // 成果を出口へ入れる。確認と同じ模擬で積むので、確認を通った成果は必ず入る
        // Insert results into the outlets; the same simulation as the check is used, so checked results always fit
        internal void InsertOutputs(IReadOnlyList<IItemStack> outputs)
        {
            CheckDestroy(this);
            if (!SpaceElevatorOutputSimulation.TryStack(CopyOutlets(), outputs, out var simulated))
            {
                Debug.LogError("[SpaceElevatorBase] 渡す直前の出口の確認を通ったのに出口へ入らず、成果を格納できません");
                throw new InvalidOperationException("Space elevator outputs no longer fit");
            }
            for (var i = 0; i < simulated.Count; i++)
            {
                var current = _itemDataStoreService.GetItem(FirstOutputSlot + i);
                // silent-ok: 不変スロットの通知を抑える / 同じ在庫値は既にUIへ通知済み
                if (simulated[i].Id == current.Id && simulated[i].Count == current.Count) continue;
                _itemDataStoreService.SetItem(FirstOutputSlot + i, simulated[i]);
            }
        }

        public void Update()
        {
            CheckDestroy(this);
            for (var slot = FirstOutputSlot; slot < _slotCount; slot++)
            {
                var output = _itemDataStoreService.GetItem(slot);
                // silent-ok: 空スロットには搬出物が無い / 入口と出口UIに空状態が表示される
                if (output.Count == 0) continue;
                _itemDataStoreService.SetItem(slot, _blockInventoryInserter.InsertItem(output));
            }
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
                if (result.RemainderItemStack.Count > 0) return false;
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

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { return InsertItem(itemStack); }
        public IItemStack InsertItem(ItemId itemId, int count) { return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count)); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { var remainders = new List<IItemStack>(); foreach (var itemStack in itemStacks) remainders.Add(InsertItem(itemStack)); return remainders; }
        public void SetItem(int slot, IItemStack itemStack) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemStack); }
        public void SetItem(int slot, ItemId itemId, int count) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemId, count); }
        public IItemStack GetItem(int slot) { CheckDestroy(this); return _itemDataStoreService.GetItem(slot); }
        public int GetSlotSize() { CheckDestroy(this); return _itemDataStoreService.GetSlotSize(); }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { CheckDestroy(this); return _itemDataStoreService.CreateCopiedItems(); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemId, count); }
        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}

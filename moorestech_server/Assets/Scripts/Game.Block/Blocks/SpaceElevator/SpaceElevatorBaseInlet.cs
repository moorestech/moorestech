using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点の入口（スロット0）
    // - 選んだ積荷だけを受け入れる
    // - 選んだ種類以外が残ると入口が塞がる
    // - 選び直しで外れた中身を返却先へ移す
    // The base inlet (slot 0)
    // - Accepts only the selected payload
    // - Blocks while a kind other than the selected one remains
    // - Moves contents dropped by a reselection to the refund target
    internal class SpaceElevatorBaseInlet : IOrbitalCargoInletSlots
    {
        private const int Slot = SpaceElevatorBaseInventoryComponent.SatelliteSlot;
        public IReadOnlyCollection<ItemId> CarriedPayloadItemIds => _countPerLaunchByCarriedPayload.Keys;
        // 在庫か選択が変わると入口の待機条件が変わる
        // Inlet waiting conditions change with the inventory or the selection
        public IObservable<Unit> OnConditionChanged => _inventoryChanged.Merge(_selection.OnChanged);

        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly Dictionary<ItemId, int> _countPerLaunchByCarriedPayload;
        private readonly IOrbitalCargoSelectionReader _selection;
        private readonly IObservable<Unit> _inventoryChanged;

        public SpaceElevatorBaseInlet(OpenableInventoryItemDataStoreService slots, Dictionary<ItemId, int> countPerLaunchByCarriedPayload, IOrbitalCargoSelectionReader selection, IObservable<Unit> inventoryChanged)
        {
            _slots = slots;
            _countPerLaunchByCarriedPayload = countPerLaunchByCarriedPayload;
            _selection = selection;
            _inventoryChanged = inventoryChanged;
        }

        public bool Accepts(ItemId itemId) => _selection.Current.IsSelected(itemId);

        // 入口の中身を見る。運べない品はマスタ変更後のロードで、選んでいない種類は返しきれなかった選び直しで残る
        // Peek at the inlet; a non-carried item remains after a master change, a non-selected kind after an incomplete reselection refund
        public SatelliteInletState Peek(out ItemId satelliteId)
        {
            var stack = _slots.GetItem(Slot);
            satelliteId = stack.Id;
            if (stack.Count == 0) return SatelliteInletState.MissingPayload;
            if (!_countPerLaunchByCarriedPayload.TryGetValue(stack.Id, out var countPerLaunch)) return SatelliteInletState.NotCarried;
            if (!Accepts(stack.Id)) return SatelliteInletState.NotSelectedKind;
            // silent-ok: 1便に満たない積荷は通常の待機 / プレイヤーは拠点UIの積荷待ちと入口の個数で気づく
            return stack.Count < countPerLaunch ? SatelliteInletState.MissingPayload : SatelliteInletState.Carriable;
        }

        // 昇っていないときの状態。未選択・入口の塞がり・積荷待ちの順に決める
        // The status while not climbing, decided as unselected, blocked inlet, then waiting for payload
        public SpaceElevatorBaseStatus WaitStatus()
        {
            if (_selection.Current is UnselectedOrbitalCargo) return SpaceElevatorBaseStatus.CargoNotSelected;
            return SatelliteInletWait.BlocksInlet(Peek(out _)) ? SpaceElevatorBaseStatus.InletBlocked : SpaceElevatorBaseStatus.WaitingSatellite;
        }

        public ItemId? SelectedCargoItemId() => _selection.Current.ToNullablePayloadItemId();

        public IItemStack Insert(IItemStack itemStack)
        {
            // silent-ok: 選んだ積荷以外はベルト上・手元に残す通常の受け入れ拒否 / プレイヤーは拠点UIの選択と入口で気づく
            if (!Accepts(itemStack.Id)) return itemStack;
            var result = _slots.GetItem(Slot).AddItem(itemStack);
            _slots.SetItem(Slot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            var slot = _slots.GetItem(Slot);
            foreach (var itemStack in itemStacks)
            {
                // silent-ok: 搬送の事前確認は在庫を変えない / 呼び出し側が返値で詰まりを認識する
                if (!Accepts(itemStack.Id)) return false;
                var result = slot.AddItem(itemStack);
                // silent-ok: 満杯の通常拒否 / 呼び出し側と入口UIに残数が届く
                if (0 < result.RemainderItemStack.Count) return false;
                slot = result.ProcessResultItemStack;
            }
            return true;
        }

        public void RefundUnselectedPayloads(SelectedOrbitalCargo selection, IOpenableInventory refundInventory)
        {
            var stack = _slots.GetItem(Slot);
            // silent-ok: 空か選んだ種類なら返すものは無い / 返却なしが正しい結果
            if (stack.Count == 0 || selection.IsSelected(stack.Id)) return;
            // 入らない分は入口に残り、取り出すまで塞がりとして表示される（ADR 0106 7）
            // What does not fit stays in the inlet and shows as blocked until taken out (ADR 0106 7)
            _slots.SetItem(Slot, refundInventory.InsertItem(stack));
        }
    }
}

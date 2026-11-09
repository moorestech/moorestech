using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Master;
using UniRx;

namespace Client.Tests.Ui.State.Inventory
{
    /// <summary>
    /// 持ち物の口のテスト用の実装。コントローラと同じ移動の規則で配列を書き換え、呼び出しを記録する
    /// Test double for the inventory port; rewrites arrays with the controller's move rules and records calls
    /// </summary>
    public sealed class RecordingPlayerInventoryOperations : IPlayerInventoryOperations
    {
        private static readonly SlotItem Empty = new(ItemMaster.EmptyItemId, 0);
        private readonly SlotItem[] _main;
        private readonly SlotItem[] _equipment;
        private readonly Subject<Unit> _onGrabChanged = new();
        private SlotItem _grab = Empty;

        // 呼ばれた移動を「from→to x個」の文字で残す（grab は "Grab"）
        // Records each move as "from->to xN" text ("Grab" for the grab)
        public List<string> Moves { get; } = new();
        public List<string> Collects { get; } = new();
        public IObservable<Unit> OnGrabChanged => _onGrabChanged;

        public RecordingPlayerInventoryOperations(int mainSlotCount, int equipmentSlotCount)
        {
            _main = new SlotItem[mainSlotCount];
            _equipment = new SlotItem[equipmentSlotCount];
            Array.Fill(_main, Empty);
            Array.Fill(_equipment, Empty);
        }

        public void SetMain(int slot, SlotItem item)
        {
            _main[slot] = item;
        }

        public void SetEquipment(int slot, SlotItem item)
        {
            _equipment[slot] = item;
        }

        public void SetGrab(SlotItem item)
        {
            _grab = item;
            _onGrabChanged.OnNext(Unit.Default);
        }

        public SlotItem ReadMain(int slot)
        {
            return _main[slot];
        }

        public PlayerInventorySnapshot ReadSnapshot()
        {
            return new PlayerInventorySnapshot((SlotItem[])_main.Clone(), _grab, (SlotItem[])_equipment.Clone());
        }

        public SlotItem ReadGrab()
        {
            return _grab;
        }

        public PlayerItemMoveOutcome MoveToGrab(PlayerSlotRef from, int count)
        {
            Moves.Add($"{from}->Grab x{count}");
            return Move(Read(from), item => Write(from, item), _grab, SetGrabSilently, count);
        }

        public PlayerItemMoveOutcome MoveFromGrab(PlayerSlotRef to, int count)
        {
            Moves.Add($"Grab->{to} x{count}");
            return Move(_grab, SetGrabSilently, Read(to), item => Write(to, item), count);
        }

        public PlayerItemMoveOutcome MoveBetweenSlots(PlayerSlotRef from, PlayerSlotRef to, int count)
        {
            Moves.Add($"{from}->{to} x{count}");
            if (from == to) return PlayerItemMoveOutcome.NoEffect;
            return Move(Read(from), item => Write(from, item), Read(to), item => Write(to, item), count);
        }

        public void CollectToGrab()
        {
            Collects.Add("Grab");
        }

        public void CollectToSlot(PlayerSlotRef slot)
        {
            Collects.Add(slot.ToString());
        }

        // コントローラの MoveItem と同じ（同一スロットは MoveBetweenSlots で NoEffect）: 空・不足は拒否、別 ID への全量は入れ替え、0 個以下・別 ID への部分は NoEffect、それ以外は加算
        // Same as the controller's MoveItem (a same-slot move is NoEffect in MoveBetweenSlots): reject empty/short, swap a full move onto another id, NoEffect for count<=0 or a partial one, else add
        private PlayerItemMoveOutcome Move(SlotItem source, Action<SlotItem> writeSource, SlotItem target, Action<SlotItem> writeTarget, int count)
        {
            if (source.IsEmpty) return PlayerItemMoveOutcome.SourceEmpty;
            if (source.Count < count) return PlayerItemMoveOutcome.SourceCountShort;
            if (count <= 0) return PlayerItemMoveOutcome.NoEffect;
            var differentId = !target.IsEmpty && target.ItemId != source.ItemId;
            if (differentId && count != source.Count) return PlayerItemMoveOutcome.NoEffect;
            if (differentId)
            {
                writeTarget(source);
                writeSource(target);
            }
            else
            {
                writeTarget(new SlotItem(source.ItemId, target.Count + count));
                var rest = source.Count - count;
                writeSource(rest <= 0 ? Empty : new SlotItem(source.ItemId, rest));
            }
            _onGrabChanged.OnNext(Unit.Default);
            return PlayerItemMoveOutcome.Moved;
        }

        private void SetGrabSilently(SlotItem item)
        {
            _grab = item;
        }

        private SlotItem Read(PlayerSlotRef slot)
        {
            return slot.IsEquipment ? _equipment[slot.Slot] : _main[slot.Slot];
        }

        private void Write(PlayerSlotRef slot, SlotItem item)
        {
            if (slot.IsEquipment) _equipment[slot.Slot] = item;
            else _main[slot.Slot] = item;
        }
    }
}

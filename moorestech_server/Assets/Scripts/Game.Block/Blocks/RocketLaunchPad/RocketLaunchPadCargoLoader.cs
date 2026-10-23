using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 入れ口の資材と積荷を数え、選ばれた1便ぶんを取り出してロケットに積む
    // Counts materials and payloads in the input slots and takes the chosen launch worth into a rocket
    internal class RocketLaunchPadCargoLoader
    {
        public int NextPayloadIndex { get; private set; }

        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;

        public RocketLaunchPadCargoLoader(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            _layout = layout;
            _slots = slots;
        }

        public void SetNextPayloadIndex(int nextPayloadIndex)
        {
            // 積荷表の行数がセーブ時から変わっても、負の値でも範囲内に収める（表が空でないことはマスタ検証が保証する）
            // Keep it in range for a changed table size or a negative value (master validation guarantees a non-empty table)
            var count = _layout.Payloads.Count;
            NextPayloadIndex = (nextPayloadIndex % count + count) % count;
        }

        public bool HasMaterials()
        {
            for (var i = 0; i < _layout.Materials.Count; i++)
                if (!HasRequirement(_layout.MaterialSlots[i], _layout.Materials[i])) return false;
            return true;
        }

        // 1便ぶん揃った積荷の番号を前回の次から順に返し、特定の積荷だけが飛び続けないようにする
        // Yields ready payload indices starting after the previous launch so no single payload starves the others
        public IEnumerable<int> ReadyPayloadIndices()
        {
            var count = _layout.Payloads.Count;
            for (var offset = 0; offset < count; offset++)
            {
                var index = (NextPayloadIndex + offset) % count;
                if (IsPayloadReady(index)) yield return index;
            }
        }

        public RocketCargo LoadCargo(int payloadIndex)
        {
            // 揃っていないのに呼ばれたら呼び出し側の段階管理の誤り
            // Being called while not ready is a stage-management bug in the caller
            if (!HasMaterials() || !IsPayloadReady(payloadIndex)) throw new InvalidOperationException("1便ぶんの資材と積荷が揃っていない発射台で建造が始まりました");

            var materials = new List<IItemStack>();
            for (var i = 0; i < _layout.Materials.Count; i++) materials.Add(TakeRequirement(_layout.MaterialSlots[i], _layout.Materials[i]));
            var payload = TakeRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);
            NextPayloadIndex = (payloadIndex + 1) % _layout.Payloads.Count;
            return new RocketCargo(payload, materials);

            #region Internal

            IItemStack TakeRequirement(int slot, RocketLaunchSlotRequirement requirement)
            {
                _slots.SetItem(slot, _slots.GetItem(slot).SubItem(requirement.Count));
                return ServerContext.ItemStackFactory.Create(requirement.ItemId, requirement.Count);
            }

            #endregion
        }

        // 積荷が1便ぶん揃ったかの判定はここだけが持ち、巡回とガードの両方が呼ぶ
        // The single readiness check for one launch worth of a payload, used by both the scan and the guard
        private bool IsPayloadReady(int payloadIndex)
        {
            return HasRequirement(_layout.PayloadSlot(payloadIndex), _layout.Payloads[payloadIndex]);
        }

        private bool HasRequirement(int slot, RocketLaunchSlotRequirement requirement)
        {
            var stack = _slots.GetItem(slot);
            return stack.Id == requirement.ItemId && requirement.Count <= stack.Count;
        }

    }
}

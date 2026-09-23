using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
namespace Game.Block.Blocks.BeltConveyor
{
    public sealed class SegmentBeltComponent : IBlockInventory, IBlockSaveState
    {
        internal readonly BlockPositionInfo Position;
        internal readonly BeltConveyorSlopeType SlopeType;
        internal BeltCellSaveState LoadedState { get; private set; }
        private readonly IBeltWorldMutation _world;
        internal const string SaveKeyStatic = "Game.Block.Blocks.BeltConveyor.SegmentBeltSaveComponent";
        public string SaveKey => SaveKeyStatic;
        public object GetSaveState() => _world.CaptureCell(this);
        public bool IsDestroy { get; private set; }
        internal SegmentBeltComponent(BlockPositionInfo position, BeltConveyorSlopeType slope, IBeltWorldMutation world,
            Dictionary<string, object> states)
        {
            Position = position; SlopeType = slope; _world = world;
            LoadedState = states == null ? new BeltCellSaveState(0, null, null)
                : BlockComponentStateReader.Read<BeltCellSaveState>(states, SaveKeyStatic);
            _world.Register(this);
        }
        public IItemStack InsertItem(IItemStack stack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            return _world.Insert(this, stack, context);
        }
        public IItemStack GetItem(int slot) { CheckSlot(slot); return _world.GetCellItem(this); }
        public void SetItem(int slot, IItemStack stack) { CheckSlot(slot); _world.SetCellItem(this, stack); }
        public int GetSlotSize() => 1;
        // この照会は予約を変更せず、実際の成功はInsertItemの戻り値だけで決まる。
        // This query never reserves a merge; only InsertItem's remainder confirms success.
        public bool InsertionCheck(List<IItemStack> stacks)
            => stacks.Count == 0 || stacks.Count == 1 && stacks[0].Count <= 1 && GetItem(0).Count == 0;
        internal void ClearLoadedState() => LoadedState = null;
        public void Destroy() { _world.Unregister(this); IsDestroy = true; }
        private void CheckSlot(int slot)
        {
            BlockException.CheckDestroy(this);
            if (slot != 0) throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}

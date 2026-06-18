using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Connector;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.InventoryConnectsModule;

namespace Game.Block.Blocks.BeltConveyor
{
    public class VanillaBeltConveyorComponent : IBlockInventory, IBlockSaveState, IItemCollectableBeltConveyor, IUpdatableBlockComponent, IBlockInventoryInsertableTargetState
    {
        public BeltConveyorSlopeType SlopeType { get; }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;

        private readonly VanillaBeltConveyorInventoryItem[] _inventoryItems;
        private readonly IBeltConveyorBlockInventoryInserter _blockInventoryInserter;
        private readonly VanillaBeltConveyorUpdateService _updateService;
        private readonly int _inventoryItemNum;
        private uint _ticksOfItemEnterToExit;

        public VanillaBeltConveyorComponent(int inventoryItemNum, float timeOfItemEnterToExitSeconds, IBeltConveyorBlockInventoryInserter blockInventoryInserter, BeltConveyorSlopeType slopeType)
        {
            SlopeType = slopeType;
            _inventoryItemNum = inventoryItemNum;
            _ticksOfItemEnterToExit = GameUpdater.SecondsToTicks(timeOfItemEnterToExitSeconds);
            _blockInventoryInserter = blockInventoryInserter;
            _inventoryItems = new VanillaBeltConveyorInventoryItem[inventoryItemNum];
            _updateService = new VanillaBeltConveyorUpdateService(_inventoryItems, _blockInventoryInserter);
        }

        public VanillaBeltConveyorComponent(Dictionary<string, string> componentStates, int inventoryItemNum, float timeOfItemEnterToExitSeconds, IBeltConveyorBlockInventoryInserter blockInventoryInserter, BeltConveyorSlopeType slopeType, InventoryConnects inventoryConnectors) :
            this(inventoryItemNum, timeOfItemEnterToExitSeconds, blockInventoryInserter, slopeType)
        {
            VanillaBeltConveyorInventorySerializer.LoadItems(componentStates[SaveKey], _inventoryItems, inventoryConnectors, _ticksOfItemEnterToExit);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);

            var insertIndex = GetInsertIndex();
            if (insertIndex < 0) return itemStack;

            // 搬出先コネクタを先に決め、詰まり時も経路を保持する
            // Resolve the goal connector up front so clogged items keep their route.
            var checkItems = new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemStack.Id, 1, itemStack.ItemInstanceId) };
            var goalConnector = _blockInventoryInserter.GetNextGoalConnector(checkItems);
            if (_blockInventoryInserter.HasAnyConnector && goalConnector == null) return itemStack;

            _inventoryItems[insertIndex] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId, context.TargetConnector, goalConnector, _ticksOfItemEnterToExit);
            return itemStack.SubItem(1);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            if (itemStacks.Count != 1 || itemStacks[0].Count != 1) return false;
            if (!_blockInventoryInserter.HasAnyConnector) return _inventoryItems[^1] == null;
            return true;
        }

        public bool HasInsertableSlot
        {
            get
            {
                BlockException.CheckDestroy(this);
                return GetInsertIndex() >= 0;
            }
        }

        public bool CanInsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            if (itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count <= 0) return false;
            return GetInsertIndex() >= 0;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inventoryItems.Length;
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            var itemStackFactory = ServerContext.ItemStackFactory;
            if (_inventoryItems[slot] == null) return itemStackFactory.CreatEmpty();
            return itemStackFactory.Create(_inventoryItems[slot].ItemId, 1);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            var goalConnector = _blockInventoryInserter?.GetNextGoalConnector(new List<IItemStack> { itemStack });
            _inventoryItems[slot] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId, null, goalConnector, _ticksOfItemEnterToExit);
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public string SaveKey { get; } = typeof(VanillaBeltConveyorComponent).FullName;

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return VanillaBeltConveyorInventorySerializer.SaveItems(_inventoryItems);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _updateService.Update(_ticksOfItemEnterToExit, _inventoryItemNum);
        }

        public void SetTicksOfItemEnterToExit(uint ticks)
        {
            _ticksOfItemEnterToExit = ticks;
            if (ticks == 0 || ticks == uint.MaxValue) return;

            // 停止中に投入された item だけを現在速度へ復帰させる
            // Restore only items inserted while the belt was stopped.
            foreach (var item in _inventoryItems)
            {
                item?.ResetTicksOnSpeedRecovery(ticks);
            }
        }

        private int GetInsertIndex()
        {
            if (!_blockInventoryInserter.HasAnyConnector) return _inventoryItems[^1] == null ? _inventoryItems.Length - 1 : -1;

            for (var i = _inventoryItems.Length - 1; i >= 0; i--)
            {
                if (_inventoryItems[i] == null) return i;
            }

            return -1;
        }
    }
}

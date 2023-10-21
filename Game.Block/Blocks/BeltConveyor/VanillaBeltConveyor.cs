using System;
using System.Collections.Generic;
using System.Text;
using Core.Item;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Interface;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     
    /// </summary>
    public class VanillaBeltConveyor : IBlock, IUpdatable, IBlockInventory
    {
        private readonly int _inventoryItemNum;
        private readonly List<BeltConveyorInventoryItem> _inventoryItems = new();
        private readonly ItemStackFactory _itemStackFactory;

        public readonly double TimeOfItemEnterToExit; 
        private IBlockInventory _connector;

        public VanillaBeltConveyor(int blockId, int entityId, ulong blockHash, ItemStackFactory itemStackFactory, int inventoryItemNum, int timeOfItemEnterToExit)
        {
            EntityId = entityId;
            BlockId = blockId;
            _itemStackFactory = itemStackFactory;
            _inventoryItemNum = inventoryItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            BlockHash = blockHash;
            _connector = new NullIBlockInventory(_itemStackFactory);
            GameUpdater.RegisterUpdater(this);
        }

        public VanillaBeltConveyor(int blockId, int entityId, ulong blockHash, string state, ItemStackFactory itemStackFactory,
            int inventoryItemNum, int timeOfItemEnterToExit) : this(blockId, entityId, blockHash, itemStackFactory, inventoryItemNum, timeOfItemEnterToExit)
        {
            //state
            
            if (state == string.Empty) return;
            var stateList = state.Split(',');
            for (var i = 0; i < stateList.Length; i += 3)
            {
                var id = int.Parse(stateList[i]);
                var remainTime = double.Parse(stateList[i + 1]);
                var limitTime = double.Parse(stateList[i + 2]);
                _inventoryItems.Add(new BeltConveyorInventoryItem(id, remainTime, limitTime, ItemInstanceIdGenerator.Generate()));
            }
        }

        public IReadOnlyList<BeltConveyorInventoryItem> InventoryItems => _inventoryItems;
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }

        public event Action<ChangedBlockState> OnBlockStateChange;

        public string GetSaveState()
        {
            if (_inventoryItems.Count == 0) return string.Empty;

            //state ItemId,RemainingTime,LimitTime,InstanceId...
            var state = new StringBuilder();
            foreach (var t in _inventoryItems)
            {
                state.Append(t.ItemId);
                state.Append(',');
                state.Append(t.RemainingTime);
                state.Append(',');
                state.Append(t.LimitTime);
                state.Append(',');
            }

            
            state.Remove(state.Length - 1, 1);
            return state.ToString();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            lock (_inventoryItems)
            {
                
                if ((1 > _inventoryItems.Count || _inventoryItems.Count >= _inventoryItemNum ||
                     !(_inventoryItems[0].RemainingTime <
                       TimeOfItemEnterToExit - TimeOfItemEnterToExit / _inventoryItemNum)) &&
                    _inventoryItems.Count != 0)
                    
                    return itemStack;


                
                if (_inventoryItems.Count == 0)
                {
                    _inventoryItems.Add(
                        new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, 0, itemStack.ItemInstanceId));
                }
                else
                {
                    

                    //index
                    _inventoryItems.Add(new BeltConveyorInventoryItem(0, 0, 0, 0));
                    
                    for (var i = _inventoryItems.Count - 1; i >= 1; i--) _inventoryItems[i] = _inventoryItems[i - 1];

                    _inventoryItems[0] = new BeltConveyorInventoryItem(
                        itemStack.Id,
                        TimeOfItemEnterToExit,
                        _inventoryItems[1].RemainingTime + TimeOfItemEnterToExit / _inventoryItemNum, itemStack.ItemInstanceId);
                }
            }

            return itemStack.SubItem(1);
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            _connector = blockInventory;
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            if (_connector.GetHashCode() == blockInventory.GetHashCode()) _connector = new NullIBlockInventory(_itemStackFactory);
        }


        public int GetSlotSize()
        {
            return _inventoryItems.Count;
        }

        public IItemStack GetItem(int slot)
        {
            return _itemStackFactory.Create(_inventoryItems[slot].ItemId, 1);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            lock (_inventoryItems)
            {
                var limitTime = _inventoryItems[slot].RemainingTime;
                _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, limitTime, itemStack.ItemInstanceId);
            }
        }


        ///     
        ///     Update
        ///     TODO 々

        public void Update()
        {
            lock (_inventoryItems)
            {
                
                if (2 <= _inventoryItems.Count)
                    //2
                    //  /  
                    for (var i = 0; i < _inventoryItems.Count - 1; i++)
                        _inventoryItems[i].LimitTime =
                            _inventoryItems[i + 1].RemainingTime + TimeOfItemEnterToExit / _inventoryItemNum;
                if (_inventoryItems.Count != 0)
                    //0
                    _inventoryItems[^1].LimitTime = 0;

                
                foreach (var t in _inventoryItems) t.RemainingTime -= GameUpdater.UpdateMillSecondTime;


                //0
                if (1 <= _inventoryItems.Count && _inventoryItems[^1].RemainingTime <= 0)
                {
                    var item = _itemStackFactory.Create(_inventoryItems[^1].ItemId, 1, _inventoryItems[^1].ItemInstanceId);
                    var output = _connector.InsertItem(item);
                    //nullItem
                    if (output.Count == 0) _inventoryItems.RemoveAt(_inventoryItems.Count - 1);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using Core.Block.BlockInventory;
using Core.Item;
using Core.Update;

namespace Core.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyor : IBlock, IUpdate, IBlockInventory
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        
        private readonly int _inventoryItemNum;
        private readonly double _timeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間

        private readonly List<BeltConveyorInventoryItem> _inventoryItems = new();
        private IBlockInventory _connector;
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaBeltConveyor(int blockId, int entityId, ulong blockHash, ItemStackFactory itemStackFactory, int inventoryItemNum, int timeOfItemEnterToExit)
        {
            EntityId = entityId;
            BlockId = blockId;
            _itemStackFactory = itemStackFactory;
            _inventoryItemNum = inventoryItemNum;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
            BlockHash = blockHash;
            _connector = new NullIBlockInventory(_itemStackFactory);
            GameUpdate.AddUpdateObject(this);
        }

        public VanillaBeltConveyor(int blockId, int entityId, ulong blockHash, string state, ItemStackFactory itemStackFactory,
            int inventoryItemNum, int timeOfItemEnterToExit) : this(blockId, entityId, blockHash, itemStackFactory, inventoryItemNum,timeOfItemEnterToExit)
        {
            //stateから復元
            //データがないときは何もしない
            if (state == String.Empty) return;
            var stateList = state.Split(',');
            for (int i = 0; i < stateList.Length; i += 3)
            {
                var id = int.Parse(stateList[i]);
                var remainTime = double.Parse(stateList[i + 1]);
                var limitTime = double.Parse(stateList[i + 2]);
                _inventoryItems.Add(new BeltConveyorInventoryItem(id, remainTime, limitTime));
            }
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //新しく挿入可能か
            if (1 <= _inventoryItems.Count && _inventoryItems.Count < _inventoryItemNum &&
                _inventoryItems[0].RemainingTime <
                _timeOfItemEnterToExit - _timeOfItemEnterToExit / _inventoryItemNum ||
                _inventoryItems.Count == 0)
            {
                if (_inventoryItems.Count == 0)
                {
                    _inventoryItems.Add(
                        new BeltConveyorInventoryItem(itemStack.Id, _timeOfItemEnterToExit, 0));
                }
                else
                {
                    //インデックスをずらす

                    //indexエラーにならないためにダミーアイテムを追加しておく
                    _inventoryItems.Add(new BeltConveyorInventoryItem(0, 0, 0));
                    //アイテムをずらす
                    for (int i = _inventoryItems.Count - 1; i >= 1; i--)
                    {
                        _inventoryItems[i] = _inventoryItems[i - 1];
                    }

                    _inventoryItems[0] = new BeltConveyorInventoryItem(
                        itemStack.Id,
                        _timeOfItemEnterToExit,
                        _inventoryItems[1].RemainingTime + (_timeOfItemEnterToExit / _inventoryItemNum));
                }

                return itemStack.SubItem(1);
            }

            return itemStack;
        }

        public void AddOutputConnector(IBlockInventory blockInventory) { _connector = blockInventory; }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            if (_connector.GetHashCode() == blockInventory.GetHashCode())
            {
                _connector = new NullIBlockInventory(_itemStackFactory);
            }
        }

        /// <summary>
        /// アイテムの搬出判定を行う
        /// 判定はUpdateで毎フレーム行われる
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {
            //リミットの更新
            if (2 <= _inventoryItems.Count)
            {
                for (int i = 0; i < _inventoryItems.Count - 1; i++)
                {
                    _inventoryItems[i].LimitTime =
                        _inventoryItems[i + 1].RemainingTime + _timeOfItemEnterToExit / _inventoryItemNum;
                }

                _inventoryItems[^1].LimitTime = 0;
            }

            //時間を減らす
            foreach (var t in _inventoryItems)
            {
                t.RemainingTime -= GameUpdate.UpdateTime;
            }


            //最後のアイテムが0だったら接続先に渡す
            if (1 <= _inventoryItems.Count && _inventoryItems[^1].RemainingTime <= 0)
            {
                var output = _connector.InsertItem(_itemStackFactory.Create(_inventoryItems[^1].ItemId, 1));
                //渡した結果がnullItemだったらそのアイテムを消す
                if (output.Count == 0)
                {
                    _inventoryItems.RemoveAt(_inventoryItems.Count - 1);
                }
            }
        }

        public string GetSaveState()
        {
            if (_inventoryItems.Count == 0) return String.Empty;

            //stateの定義 itemid1,RemainingTime1,LimitTime1,itemid2,RemainingTime2,LimitTime2,itemid3,RemainingTime3,LimitTime3...
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

            //最後のカンマを削除
            state.Remove(state.Length - 1, 1);
            return state.ToString();
        }
        
        
        public int GetSlotSize() { return _inventoryItems.Count; }
        public IItemStack GetItem(int slot) { return _itemStackFactory.Create(_inventoryItems[slot].ItemId, 1); }
        public void SetItem(int slot, IItemStack itemStack)
        {
            var limitTime = _inventoryItems[slot].RemainingTime;
            _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, _timeOfItemEnterToExit, limitTime);
        }
    }
}
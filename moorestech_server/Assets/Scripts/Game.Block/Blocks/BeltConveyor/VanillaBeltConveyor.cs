using System;
using System.Collections.Generic;
using System.Text;
using Core.Const;
using Core.Item;
using Core.Update;
using Game.Block.Base;
using Game.Block.BlockInventory;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyor : IBlock, IBlockInventory
    {

        public readonly double TimeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間
        public readonly int InventoryItemNum;
        
        private readonly BeltConveyorInventoryItem[] _inventoryItems;
        private readonly ItemStackFactory _itemStackFactory;
        
        private IBlockInventory _connector;

        public VanillaBeltConveyor(int blockId, int entityId, long blockHash, ItemStackFactory itemStackFactory,
            int inventoryItemNum, int timeOfItemEnterToExit)
        {
            EntityId = entityId;
            BlockId = blockId;
            _itemStackFactory = itemStackFactory;
            InventoryItemNum = inventoryItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            BlockHash = blockHash;
            _connector = new NullIBlockInventory(_itemStackFactory);

            _inventoryItems = new BeltConveyorInventoryItem[inventoryItemNum];
            
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public VanillaBeltConveyor(int blockId, int entityId, long blockHash, string state,
            ItemStackFactory itemStackFactory,
            int inventoryItemNum, int timeOfItemEnterToExit) : this(blockId, entityId, blockHash, itemStackFactory,
            inventoryItemNum, timeOfItemEnterToExit)
        {
            //stateから復元
            //データがないときは何もしない
            if (state == string.Empty) return;
            var stateList = state.Split(',');
            for (var i = 0; i < _inventoryItems.Length; i++)
            {
                var saveIndex = i * 2;
                var id = int.Parse(stateList[saveIndex]);
                var remainTime = double.Parse(stateList[saveIndex + 1]);
                if (id == -1) continue;
                
                _inventoryItems[i] = new BeltConveyorInventoryItem(id, remainTime, ItemInstanceIdGenerator.Generate());
            }
        }

        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }

        public event Action<ChangedBlockState> OnBlockStateChange;

        public string GetSaveState()
        {
            if (_inventoryItems.Length == 0) return string.Empty;

            //stateの定義 ItemId,RemainingTime,LimitTime,InstanceId...
            var state = new StringBuilder();
            foreach (var t in _inventoryItems)
            {
                if (t == null)
                {
                    state.Append("-1,-1,");
                    continue;
                }
                state.Append(t.ItemId);
                state.Append(',');
                state.Append(t.RemainingTime);
                state.Append(',');
            }

            //最後のカンマを削除
            state.Remove(state.Length - 1, 1);
            return state.ToString();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //新しく挿入可能か
            if (_inventoryItems[^1] != null)
                //挿入可能でない
                return itemStack;

            _inventoryItems[^1] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, itemStack.ItemInstanceId);
            
            //挿入したのでアイテムを減らして返す
            return itemStack.SubItem(1);
        }

        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            _connector = blockInventory;
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            if (_connector.GetHashCode() == blockInventory.GetHashCode())
                _connector = new NullIBlockInventory(_itemStackFactory);
        }


        public int GetSlotSize()
        {
            return _inventoryItems.Length;
        }

        public IItemStack GetItem(int slot)
        {
            return _itemStackFactory.Create(_inventoryItems[slot].ItemId, 1);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            //TODo lockすべき？？
            
            _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit,
                itemStack.ItemInstanceId);
        }

        /// <summary>
        ///     アイテムの搬出判定を行う
        ///     判定はUpdateで毎フレーム行われる
        ///     TODO 個々のマルチスレッド対応もいい感じにやりたい
        /// </summary>
        private void Update()
        {
            //TODO lockすべき？？
            var count = _inventoryItems.Length;

            for (int i = 0; i < count; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;
                
                //次のインデックスに入れる時間かどうかをチェックする
                var nextIndexStartTime = i * (TimeOfItemEnterToExit / InventoryItemNum);
                var isNextInsertable = item.RemainingTime <= nextIndexStartTime;
                    
                //次に空きがあれば次に移動する
                if (isNextInsertable && i != 0)
                {
                    if (_inventoryItems[i - 1] == null)
                    {
                        _inventoryItems[i - 1] = item;
                        _inventoryItems[i] = null;
                    }
                    continue;
                }
                    
                //最後のアイテムの場合は接続先に渡す
                if (i == 0 && item.RemainingTime <= 0)
                {
                    var insertItem = _itemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
                    var output = _connector.InsertItem(insertItem);
                    //渡した結果がnullItemだったらそのアイテムを消す
                    if (output.Id == ItemConst.EmptyItemId) _inventoryItems[i] = null;
                        
                    continue;
                }

                //時間を減らす 
                item.RemainingTime -= GameUpdater.UpdateMillSecondTime;
            }
        }
        
        public BeltConveyorInventoryItem GetBeltConveyorItem(int index)
        {
            return _inventoryItems[index];
        }
    }
}
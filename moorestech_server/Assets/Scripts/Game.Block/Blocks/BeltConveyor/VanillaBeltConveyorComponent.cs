﻿using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Connector;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyorComponent : IBlockInventory, IBlockSaveState, IItemCollectableBeltConveyor, IUpdatableBlockComponent
    {
        public BeltConveyorSlopeType SlopeType { get; }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        private readonly VanillaBeltConveyorInventoryItem[] _inventoryItems;
        
        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly int _inventoryItemNum;
        
        private double _timeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間
        
        public VanillaBeltConveyorComponent(int inventoryItemNum, float timeOfItemEnterToExit, IBlockInventoryInserter blockInventoryInserter, BeltConveyorSlopeType slopeType)
        {
            SlopeType = slopeType;
            _inventoryItemNum = inventoryItemNum;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
            _blockInventoryInserter = blockInventoryInserter;
            
            _inventoryItems = new VanillaBeltConveyorInventoryItem[inventoryItemNum];
        }
        
        public VanillaBeltConveyorComponent(Dictionary<string, string> componentStates, int inventoryItemNum, float timeOfItemEnterToExit, IBlockInventoryInserter blockInventoryInserter, BeltConveyorSlopeType slopeType) :
            this(inventoryItemNum, timeOfItemEnterToExit, blockInventoryInserter, slopeType)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<string>>(componentStates[SaveKey]);
            for (var i = 0; i < itemJsons.Count; i++)
            {
                if (itemJsons[i] != null)
                {
                    _inventoryItems[i] = VanillaBeltConveyorInventoryItem.LoadItem(itemJsons[i]);
                }
            }
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            //新しく挿入可能か
            if (_inventoryItems[^1] != null)
                //挿入可能でない
                return itemStack;
            
            _inventoryItems[^1] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId);
            
            //挿入したのでアイテムを減らして返す
            return itemStack.SubItem(1);
        }
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            
            // 空きスロットがない
            if (_inventoryItems[^1] != null) return false;
            
            // 挿入スロットが1個かどうか
            if (itemStacks.Count == 1 && itemStacks[0].Count == 1) return true;
            
            return false;
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
            
            //TODO lockすべき？？
            _inventoryItems[slot] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId);
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
            
            var saveItems = new List<string>();
            foreach (var t in _inventoryItems)
            {
                saveItems.Add(t?.GetSaveJsonString());
            }
            
            return JsonConvert.SerializeObject(saveItems);
        }
        
        /// <summary>
        ///     アイテムの搬出判定を行う
        ///     判定はUpdateで毎フレーム行われる
        ///     TODO 個々のマルチスレッド対応もいい感じにやりたい
        /// </summary>
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            //TODO lockすべき？？
            var count = _inventoryItems.Length;
            
            for (var i = 0; i < count; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;
                
                //次のインデックスに入れる時間かどうかをチェックする
                var nextIndexStartTime = i * (1f / _inventoryItemNum);
                var isNextInsertable = item.RemainingPercent <= nextIndexStartTime;
                
                //次に空きがあれば次に移動する
                if (isNextInsertable && i != 0)
                {
                    if (_inventoryItems[i - 1] == null)
                    {
                        _inventoryItems[i - 1] = item;
                        _inventoryItems[i] = null;
                    }
                }
                
                //最後のアイテムの場合は接続先に渡す
                if (i == 0 && item.RemainingPercent <= 0)
                {
                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
                    
                    var output = _blockInventoryInserter.InsertItem(insertItem);
                    
                    //渡した結果がnullItemだったらそのアイテムを消す
                    if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[i] = null;
                    
                    continue;
                }
                
                //時間を減らす 
                var diff = (float)(GameUpdater.UpdateSecondTime * (1f / (float)_timeOfItemEnterToExit));
                var last = item.RemainingPercent;
                item.RemainingPercent -= diff;
                var current = item.RemainingPercent;
                
                if (item.ItemInstanceId.AsPrimitive() != 0)
                {
                    //UnityEngine.Debug.Log($"Belt Last:{last:F3} Current:{current:F3} Diff:{diff:F3} {item.ItemInstanceId}");
                }
            }
        }
        
        public void SetTimeOfItemEnterToExit(double time)
        {
            _timeOfItemEnterToExit = time;
        }
    }
}
﻿using System;
using System.Text;
using Core.Const;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Component.IOConnector;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyorComponent : IBlockInventory, IBlockSaveState, IBlockStateChange
    {
        public bool IsDestroy { get; private set; }

        public const float DefaultBeltConveyorHeight = 0.3f;

        public IObservable<ChangedBlockState> BlockStateChange => _onBlockStateChange;
        private readonly Subject<ChangedBlockState> _onBlockStateChange = new();

        private readonly BeltConveyorInventoryItem[] _inventoryItems;
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;

        public readonly int InventoryItemNum;
        public readonly double TimeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間

        private readonly string _blockName;

        private readonly IDisposable _updateObservable;

        public VanillaBeltConveyorComponent(int inventoryItemNum, int timeOfItemEnterToExit, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, string blockName)
        {
            _blockName = blockName;
            InventoryItemNum = inventoryItemNum;
            TimeOfItemEnterToExit = timeOfItemEnterToExit;
            _blockConnectorComponent = blockConnectorComponent;

            _inventoryItems = new BeltConveyorInventoryItem[inventoryItemNum];

            _updateObservable = GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        public VanillaBeltConveyorComponent(string state, int inventoryItemNum, int timeOfItemEnterToExit, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, string blockName) :
            this(inventoryItemNum, timeOfItemEnterToExit, blockConnectorComponent, blockName)
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

        public string GetSaveState()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

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
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            //新しく挿入可能か
            if (_inventoryItems[^1] != null)
                //挿入可能でない
                return itemStack;

            _inventoryItems[^1] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, itemStack.ItemInstanceId);

            //挿入したのでアイテムを減らして返す
            return itemStack.SubItem(1);
        }

        public int GetSlotSize()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            return _inventoryItems.Length;
        }

        public IItemStack GetItem(int slot)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            var itemStackFactory = ServerContext.ItemStackFactory;
            if (_inventoryItems[slot] == null)
            {
                return itemStackFactory.CreatEmpty();
            }
            return itemStackFactory.Create(_inventoryItems[slot].ItemId, 1);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            //TODO lockすべき？？
            _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, itemStack.ItemInstanceId);
        }

        /// <summary>
        ///     アイテムの搬出判定を行う
        ///     判定はUpdateで毎フレーム行われる
        ///     TODO 個々のマルチスレッド対応もいい感じにやりたい
        /// </summary>
        private void Update()
        {
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);

            //TODO lockすべき？？
            var count = _inventoryItems.Length;

            if (_blockName == VanillaBeltConveyorTemplate.Hueru && _inventoryItems[0] == null)
            {
                _inventoryItems[0] = new BeltConveyorInventoryItem(4, TimeOfItemEnterToExit, ItemInstanceIdGenerator.Generate());
            }

            for (var i = 0; i < count; i++)
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
                    if (_blockName == VanillaBeltConveyorTemplate.Kieru)
                    {
                        _inventoryItems[i] = null;
                    }

                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);

                    if (_blockConnectorComponent.ConnectTargets.Count == 0) continue;

                    var connector = _blockConnectorComponent.ConnectTargets[0];
                    var output = connector.InsertItem(insertItem);


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
            if (IsDestroy) throw new InvalidOperationException(BlockException.IsDestroyed);
            return _inventoryItems[index];
        }

        public void Destroy()
        {
            IsDestroy = true;
            _updateObservable.Dispose();
        }
    }
}
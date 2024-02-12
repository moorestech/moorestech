using System;
using System.Collections.Generic;
using System.Text;
using Core.Item;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyor : IBlock, IBlockInventory
    {
        private readonly int _inventoryItemNum;
        private readonly BeltConveyorInventoryItem[] _inventoryItems;
        private readonly ItemStackFactory _itemStackFactory;

        public readonly double TimeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間
        private IBlockInventory _connector;

        public VanillaBeltConveyor(int blockId, int entityId, long blockHash, ItemStackFactory itemStackFactory,
            int inventoryItemNum, int timeOfItemEnterToExit)
        {
            EntityId = entityId;
            BlockId = blockId;
            _itemStackFactory = itemStackFactory;
            _inventoryItemNum = inventoryItemNum;
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
            for (var i = 0; i < stateList.Length; i += 3)
            {
                var id = int.Parse(stateList[i]);
                var remainTime = double.Parse(stateList[i + 1]);
                var limitTime = double.Parse(stateList[i + 2]);
                _inventoryItems[i] = new BeltConveyorInventoryItem(id, remainTime, limitTime, ItemInstanceIdGenerator.Generate());
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

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //新しく挿入可能か
            if (_inventoryItems[0] != null)
                //挿入可能でない
                return itemStack;

            _inventoryItems[0] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, 0, itemStack.ItemInstanceId);
            
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
            lock (_inventoryItems)
            {
                var limitTime = _inventoryItems[slot].RemainingTime;
                _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, limitTime,
                    itemStack.ItemInstanceId);
            }
        }

        /// <summary>
        ///     アイテムの搬出判定を行う
        ///     判定はUpdateで毎フレーム行われる
        ///     TODO 個々のマルチスレッド対応もいい感じにやりたい
        /// </summary>
        private void Update()
        {
            lock (_inventoryItems)
            {
                var count = _inventoryItems.Length;
                
                //リミットの更新
                if (2 <= count)
                    //アイテムが2個以上あるときは、次のアイテムと間隔をあけてリミットを設定する
                    //間隔値は ベルトコンベアに入るアイテム数 / アイテムが入ってから出るまでの時間 で決まる
                    for (var i = 0; i < count - 1; i++)
                        _inventoryItems[i].LimitTime =
                            _inventoryItems[i + 1].RemainingTime + TimeOfItemEnterToExit / _inventoryItemNum;
                if (count != 0)
                    //最後のアイテムは最後まで進むのでリミットは0になる
                    _inventoryItems[^1].LimitTime = 0;

                //時間を減らす
                for (var i = 0; i < count; i++)
                {
                    var t = _inventoryItems[i];
                    t.RemainingTime -= GameUpdater.UpdateMillSecondTime;
                }


                //最後のアイテムが0だったら接続先に渡す
                if (1 <= count && _inventoryItems[^1].RemainingTime <= 0)
                {
                    var item = _itemStackFactory.Create(_inventoryItems[^1].ItemId, 1,
                        _inventoryItems[^1].ItemInstanceId);
                    var output = _connector.InsertItem(item);
                    //渡した結果がnullItemだったらそのアイテムを消す
                    if (output.Count == 0) _inventoryItems.RemoveAt(count - 1);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using Core.Block.BlockInventory;
using Core.Block.Blocks.State;
using Core.Item;
using Core.Update;

namespace Core.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyor : IBlock, IUpdatable, IBlockInventory
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        
        public event Action<ChangedBlockState> OnBlockStateChange;
        
        private readonly int _inventoryItemNum;
        
        public readonly double TimeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間

        public IReadOnlyList<BeltConveyorInventoryItem> InventoryItems => _inventoryItems;
        private readonly List<BeltConveyorInventoryItem> _inventoryItems = new();
        private IBlockInventory _connector;
        private readonly ItemStackFactory _itemStackFactory;

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
                _inventoryItems.Add(new BeltConveyorInventoryItem(id, remainTime, limitTime,ItemInstanceIdGenerator.Generate()));
            }
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //新しく挿入可能か
            if ((1 > _inventoryItems.Count || _inventoryItems.Count >= _inventoryItemNum ||
                 !(_inventoryItems[0].RemainingTime <
                   TimeOfItemEnterToExit - TimeOfItemEnterToExit / _inventoryItemNum)) &&
                _inventoryItems.Count != 0)
            {
                //挿入可能でない
                return itemStack;
            }
            
            
            //アイテムをベルトコンベア内のアイテムに挿入する
            if (_inventoryItems.Count == 0)
            {
                _inventoryItems.Add(
                    new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, 0,itemStack.ItemInstanceId));
            }
            else
            {
                //インデックスをずらす

                //indexエラーにならないためにダミーアイテムを追加しておく
                _inventoryItems.Add(new BeltConveyorInventoryItem(0, 0, 0,0));
                //アイテムをずらす
                for (int i = _inventoryItems.Count - 1; i >= 1; i--)
                {
                    _inventoryItems[i] = _inventoryItems[i - 1];
                }

                _inventoryItems[0] = new BeltConveyorInventoryItem(
                    itemStack.Id,
                    TimeOfItemEnterToExit,
                    _inventoryItems[1].RemainingTime + (TimeOfItemEnterToExit / _inventoryItemNum),itemStack.ItemInstanceId);
            }

            return itemStack.SubItem(1);

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
        public void Update()
        {
            //リミットの更新
            if (2 <= _inventoryItems.Count)
            {
                //アイテムが2個以上あるときは、次のアイテムと間隔をあけてリミットを設定する
                //間隔値は ベルトコンベアに入るアイテム数 / アイテムが入ってから出るまでの時間 で決まる
                for (int i = 0; i < _inventoryItems.Count - 1; i++)
                {
                    _inventoryItems[i].LimitTime =
                        _inventoryItems[i + 1].RemainingTime + TimeOfItemEnterToExit / _inventoryItemNum;
                }
            }
            if (_inventoryItems.Count != 0)
            {
                //最後のアイテムは最後まで進むのでリミットは0になる
                _inventoryItems[^1].LimitTime = 0;
            }

            //時間を減らす
            foreach (var t in _inventoryItems)
            {
                t.RemainingTime -= GameUpdater.UpdateMillSecondTime;
            }


            //最後のアイテムが0だったら接続先に渡す
            if (1 <= _inventoryItems.Count && _inventoryItems[^1].RemainingTime <= 0)
            {
                var item = _itemStackFactory.Create(_inventoryItems[^1].ItemId, 1, _inventoryItems[^1].ItemInstanceId);
                var output = _connector.InsertItem(item);
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



        public int GetSlotSize() { return _inventoryItems.Count; }
        public IItemStack GetItem(int slot) { return _itemStackFactory.Create(_inventoryItems[slot].ItemId, 1); }
        public void SetItem(int slot, IItemStack itemStack)
        {
            var limitTime = _inventoryItems[slot].RemainingTime;
            _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, TimeOfItemEnterToExit, limitTime,itemStack.ItemInstanceId);
        }
    }
}
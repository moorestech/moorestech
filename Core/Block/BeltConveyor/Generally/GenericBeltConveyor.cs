using System;
using System.Collections.Generic;
using industrialization.Core.Config.BeltConveyor;
using industrialization.Core.GameSystem;
using industrialization.Core.Item;

namespace industrialization.Core.Block.BeltConveyor.Generally
{
    /// <summary>
    /// アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class GenericBeltConveyor :IBlock, IUpdate, IBlockInventory
    {
        private readonly int _inventoryItemNum;
        private readonly double _timeOfItemEnterToExit;//ベルトコンベアにアイテムが入って出るまでの時間

        private readonly List<BeltConveyorInventoryItem> _inventoryItems = new List<BeltConveyorInventoryItem>();
        private IBlockInventory _connector;

        public GenericBeltConveyor(int blockId, int intId, IBlockInventory connector)
        {
            _blockId = blockId;
            _intId = intId;
            _connector = connector;
            var conf = BeltConveyorConfig.GetBeltConveyorData(blockId);
            _inventoryItemNum = conf.BeltConveyorItemNum;
            _timeOfItemEnterToExit = conf.TimeOfItemEnterToExit;
            GameUpdate.AddUpdateObject(this);
        }

        public IItemStack 
        InsertItem(IItemStack itemStack)
        {
            //新しく挿入可能か
            if (1 <= _inventoryItems.Count && _inventoryItems.Count < _inventoryItemNum &&
                _inventoryItems[0].RemainingTime < _timeOfItemEnterToExit - _timeOfItemEnterToExit / _inventoryItemNum ||
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
                    _inventoryItems.Add(_inventoryItems[0]);
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

        public void ChangeConnector(IBlockInventory blockInventory)
        {
            _connector = blockInventory;
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
                for (int i = 0; i < _inventoryItems.Count-1; i++)
                {
                    _inventoryItems[i].LimitTime =
                        _inventoryItems[i + 1].RemainingTime + _timeOfItemEnterToExit / _inventoryItemNum;
                }
                _inventoryItems[_inventoryItems.Count - 1].LimitTime = 0;
            }

            //時間を減らす
            foreach (var t in _inventoryItems)
            {
                t.RemainingTime -= GameUpdate.UpdateTime;
            }


            //最後のアイテムが0だったら接続先に渡す
            var last = _inventoryItems.Count - 1;
            if (1 <= _inventoryItems.Count && _inventoryItems[last].RemainingTime <= 0)
            {
                var output = _connector.InsertItem(ItemStackFactory.NewItemStack(_inventoryItems[last].ItemId, 1));
                //渡した結果がnullItemだったらそのアイテムを消す
                if (output.Id == BlockConst.NullBlockId)
                {
                    _inventoryItems.RemoveAt(_inventoryItems.Count - 1);
                }
            }
        }

        private int _blockId;
        private int _intId;
        public int GetIntId()
        {
            throw new NotImplementedException();
        }

        public int GetBlockId()
        {
            throw new NotImplementedException();
        }
    }
}
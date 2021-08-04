using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.BeltConveyor;
using industrialization.Core.GameSystem;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.BeltConveyor.Generally
{
    /// <summary>
    /// アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class GenericBeltConveyor : IUpdate, IInstallationInventory
    {
        private readonly int _inventoryItemNum = 4;
        private readonly double _canItemInsertTime = 500;

        private readonly List<BeltConveyorInventoryItem> _inventoryItems;
        private IInstallationInventory _connector;

        public GenericBeltConveyor(int installtionID, IInstallationInventory connector)
        {
            _connector = connector;
            var conf = BeltConveyorConfig.GetBeltConveyorData(installtionID);
            _inventoryItemNum = conf.BeltConveyorItemNum;
            _canItemInsertTime = conf.BeltConveyorSpeed;
            GameUpdate.AddUpdateObject(this);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            //新しく挿入可能か
            if (_inventoryItems.Count < _inventoryItemNum &&
                _inventoryItems[0].RemainingTime < _canItemInsertTime - _canItemInsertTime / _inventoryItemNum)
            {
                if (_inventoryItems.Count == 0)
                {
                    _inventoryItems.Add(
                        new BeltConveyorInventoryItem(itemStack.Id, _canItemInsertTime, 0));
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
                        _canItemInsertTime,
                        _inventoryItems[1].RemainingTime + (_canItemInsertTime / _inventoryItemNum));
                }

                return itemStack.SubItem(1);
            }
            else
            {
                return itemStack;
            }
        }

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            _connector = installationInventory;
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
                for (int i = 0; i < _inventoryItems.Count; i++)
                {
                    _inventoryItems[i].LimitTime =
                        _inventoryItems[i + 1].RemainingTime + _canItemInsertTime / _inventoryItemNum;
                }
            }

            //時間を減らす
            foreach (var t in _inventoryItems)
            {
                t.RemainingTime -= GameUpdate.UpdateTime;
            }


            //最後のアイテムが0だったら接続先に渡す
            if (_inventoryItems[^1].RemainingTime <= 0)
            {
                var output = _connector.InsertItem(ItemStackFactory.NewItemStack(_inventoryItems[^1].ItemId, 1));
                //渡した結果がnullItemだったらそのアイテムを消す
                if (output.Id == NullItemStack.NullItemId)
                {
                    _inventoryItems.RemoveAt(_inventoryItems.Count - 1);
                }
            }
        }
    }
}
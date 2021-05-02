using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using industrialization.GameSystem;
using industrialization.Installation.BeltConveyor.Generally.DataClass;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    /// <summary>
    /// アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class GenericBeltConveyorInventory : IBeltConveyorComponent,IUpdate
    {
        private const int InventoryItemNum = 4;
        private const double CanItemInsertTime = 0.5;
        
        private readonly IBeltConveyorComponent _beltConveyorConnector;
        private readonly List<GenericBeltConveyorInventoryItem> _inventoryItems;

        public GenericBeltConveyorInventory(IBeltConveyorComponent beltConveyorConnector)
        {
            _beltConveyorConnector = beltConveyorConnector;
            _inventoryItems = new List<GenericBeltConveyorInventoryItem>();
            GameUpdate.AddUpdate(this);
        }

        /// <summary>
        /// アイテムを搬入する
        /// </summary>
        /// <param name="item">搬入したいアイテム</param>
        /// <returns>搬入に成功したらtrue、失敗したらfalseを返す</returns>
        public bool InsertItem(IItemStack item)
        {
            //インベントリのアイテムの中で一番新しく入ったアイテムが、指定時間立っていなかったらfalseを返す
            if (DateTime.Now < 
                _inventoryItems.
                    Max(i => i.InsertTime).
                    AddSeconds(CanItemInsertTime)) return false;
            //インベントリ内のアイテムが指定個数かそれ以上ならfalseを返す
            if (InventoryItemNum <= _inventoryItems.Count) return false;

            
            //上記の条件を満たさない時、インベントリにアイテムを加える
            _inventoryItems.Add(new GenericBeltConveyorInventoryItem(item.Id,CanItemInsertTime));
            return true;
        }

        /// <summary>
        /// アイテムの搬出判定を行う
        /// 判定はUpdateで毎フレーム行われる
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {
            if (!ItemOutputAvailable())return;
            if(_inventoryItems.Count <= 0) return;
            var minTime = _inventoryItems.Min(i => i.RemovalAvailableTime);
            if (DateTime.Now < minTime)return;

            
            //最も古いアイテムのインデックスを取得
            int oldindex = 0;
            for (int i = 0; i < _inventoryItems.Count; i++)
            {
                if (!_inventoryItems[i].RemovalAvailableTime.Equals(minTime)) continue;
                oldindex = i;
                break;
            }

            //アイテムをインサートを試す
            var tmpItem = new ItemStack(_inventoryItems[oldindex].ItemID, GenericBeltConveyor.CanCarryItemNum);
            if (!_beltConveyorConnector.InsertItem(tmpItem))return;
            
            _inventoryItems.Remove(_inventoryItems[oldindex]);
        }

        private static bool ItemOutputAvailable()
        {
            throw new System.NotImplementedException();
        }
    }
}
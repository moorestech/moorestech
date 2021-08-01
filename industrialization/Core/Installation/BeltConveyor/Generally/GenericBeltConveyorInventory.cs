using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.BeltConveyor;
using industrialization.Core.GameSystem;
using industrialization.Core.Installation.BeltConveyor.Generally.DataClass;
using industrialization.Core.Installation.BeltConveyor.Interface;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.BeltConveyor.Generally
{
    /// <summary>
    /// アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class GenericBeltConveyorInventory : IBeltConveyorComponent,IUpdate
    {
        private readonly int InventoryItemNum = 4;
        private readonly double CanItemInsertTime = 500;
        
        private readonly IBeltConveyorComponent _beltConveyorConnector;
        private readonly List<GenericBeltConveyorInventoryItem> _inventoryItems;

        public GenericBeltConveyorInventory(int installtionID,IBeltConveyorComponent beltConveyorConnector)
        {
            var conf = BeltConveyorConfig.GetBeltConveyorData(installtionID);
            InventoryItemNum = conf.BeltConveyorItemNum;
            CanItemInsertTime = conf.BeltConveyorSpeed;
            _beltConveyorConnector = beltConveyorConnector;
            _inventoryItems = new List<GenericBeltConveyorInventoryItem>();
            GameUpdate.AddUpdateObject(this);
        }

        /// <summary>
        /// アイテムを搬入する
        /// </summary>
        /// <param name="item">搬入したいアイテム</param>
        /// <returns>搬入に成功したらtrue、失敗したらfalseを返す</returns>
        public bool InsertItem(IItemStack item)
        {
            //インベントリのアイテムの中で一番新しく入ったアイテムが、指定時間立っていなかったらfalseを返す
            if (0 < _inventoryItems.Count && DateTime.Now < 
                _inventoryItems.
                    Max(i => i.InsertTime).
                    AddMilliseconds(CanItemInsertTime)) return false;
            //インベントリ内のアイテムが指定個数かそれ以上ならfalseを返す
            if (InventoryItemNum <= _inventoryItems.Count) return false;

            
            //上記の条件を満たさない時、インベントリにアイテムを加える
            _inventoryItems.Add(new GenericBeltConveyorInventoryItem(item.Id,CanItemInsertTime));
            return true;
        }

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
            _beltConveyorConnector.ChangeConnector(installationInventory);
        }

        /// <summary>
        /// アイテムの搬出判定を行う
        /// 判定はUpdateで毎フレーム行われる
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {
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
    }
}
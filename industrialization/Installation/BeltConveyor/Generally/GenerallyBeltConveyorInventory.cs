using System;
using System.Collections.Generic;
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
    public class GenerallyBeltConveyorInventory : IBeltConveyorComponent,IUpdate
    {
        private const int InventoryItemNum = 4;
        private const double CanItemInsertTime = 0.5;
        
        private readonly IBeltConveyorComponent _beltConveyorConnector;
        private readonly List<GenerallyBeltConveyorInventoryItem> _inventoryItems;

        public GenerallyBeltConveyorInventory(IBeltConveyorComponent beltConveyorConnector)
        {
            _beltConveyorConnector = beltConveyorConnector;
            _inventoryItems = new List<GenerallyBeltConveyorInventoryItem>();
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
            _inventoryItems.Add(new GenerallyBeltConveyorInventoryItem(item.Id,CanItemInsertTime));
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
            if (!_beltConveyorConnector.InsertItem(new NullItemStack()))return;
            if (DateTime.Now < _inventoryItems.Min(i => i.RemovalAvailableTime))return;

            

            


            _beltConveyorConnector.InsertItem();
        }

        private static bool ItemOutputAvailable()
        {
            throw new System.NotImplementedException();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.GameSystem;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyorInventory : IBeltConveyorItemInventory,IUpdate
    {
        private const int InventoryItemNum = 4;
        private const double CanItemInsertTime = 0.5;
        
        private readonly IBeltConveyorConnector _beltConveyorConnector;
        private List<GenerallyBeltConveyorInventoryItem> _inventoryItems;

        public GenerallyBeltConveyorInventory(IBeltConveyorConnector beltConveyorConnector)
        {
            _beltConveyorConnector = beltConveyorConnector;
            _inventoryItems = new List<GenerallyBeltConveyorInventoryItem>();
            GameUpdate.AddUpdate(this);
        }

        public bool InsertItem(IItemStack item)
        {
            if (_inventoryItems.Max(i => i.InsertTime))
            {
                
            }
            throw new System.NotImplementedException();
        }

        public void Update()
        {
            if (!ItemOutputAvailable())return;
            if (!_beltConveyorConnector.ConnectInsert(new NullItemStack()))return;

            
            
            //TODO アイテムを移す処理を書く
            
            throw new System.NotImplementedException();
        }

        private static bool ItemOutputAvailable()
        {
            throw new System.NotImplementedException();
        }
    }
}
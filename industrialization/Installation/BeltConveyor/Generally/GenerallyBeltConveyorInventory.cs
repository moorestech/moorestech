using industrialization.GameSystem;
using industrialization.Installation.BeltConveyor.Interface;
using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyorInventory : IBeltConveyorItemInventory,IUpdate
    {
        private readonly IBeltConveyorConnector _beltConveyorConnector;

        public GenerallyBeltConveyorInventory(IBeltConveyorConnector beltConveyorConnector)
        {
            _beltConveyorConnector = beltConveyorConnector;
            GameUpdate.AddUpdate(this);
        }

        public bool InsertItem(IItemStack item)
        {
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
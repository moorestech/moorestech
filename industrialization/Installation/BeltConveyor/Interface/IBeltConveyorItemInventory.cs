using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Interface
{
    public interface IBeltConveyorItemInventory
    {
        public bool InsertItem(IItemStack item);
    }
}
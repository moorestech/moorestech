using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Interface
{
    public interface IBeltConveyorComponent
    {
        public bool InsertItem(IItemStack item);
    }
}
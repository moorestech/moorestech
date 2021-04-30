using industrialization.Item;

namespace industrialization.Installation.BeltConveyor.Interface
{
    public interface IBeltConveyorConnector
    {
        public bool ConnectInsert(IItemStack item);
    }
}
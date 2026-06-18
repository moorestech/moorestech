using Core.Item.Interface;

namespace Game.Block.Blocks.Connector
{
    public interface IBlockInventoryInserter
    {
        public IItemStack InsertItem(IItemStack itemStack);
    }

    public interface IBlockInventoryInsertTargetState
    {
        public bool CanInsertToNextTarget();
        public bool CanInsertItemToNextTarget(IItemStack itemStack);
    }

    public interface IBlockInventoryInsertableTargetState
    {
        public bool HasInsertableSlot { get; }
        public bool CanInsertItem(IItemStack itemStack);
    }

    public interface IBlockInventoryFastInsertTarget : IBlockInventoryInsertableTargetState
    {
        public int InventoryVersion { get; }
        public IItemStack InsertItemFast(IItemStack itemStack);
    }
}

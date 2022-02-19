using Core.Block.Blocks;
using Core.Item;
using Game.World.Interface.DataStore;

namespace Game.World.Interface.Event
{
    public class BlockInventoryUpdateEventProperties
    {
        public readonly Coordinate Coordinate;
        public readonly IBlock Block;
        public readonly int Slot;
        public readonly IItemStack ItemStack;

        public BlockInventoryUpdateEventProperties(IItemStack itemStack, int slot, IBlock block, Coordinate coordinate)
        {
            ItemStack = itemStack;
            Slot = slot;
            Block = block;
            Coordinate = coordinate;
        }
    }
}
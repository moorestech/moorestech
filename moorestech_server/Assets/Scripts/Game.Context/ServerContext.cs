using Core.Item.Interface;
using Core.Item.Config;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Event;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;

namespace Game.Context
{
    public class ServerContext
    {
        public static IItemConfig ItemConfig { get; private set; }
        public static IBlockConfig BlockConfig { get; private set; }
        public static ICraftingConfig CraftingConfig { get; private set; }
        public static IMachineRecipeConfig MachineRecipeConfig { get; private set; } //TODO これをブロックコンフィグに統合する
        
        public static IItemStackFactory IItemStackFactory { get; private set; }
        public static IBlockFactory BlockFactory { get; private set; }
        
        public static IWorldBlockDatastore WorldBlockDatastore { get; private set; }
        
        public static IWorldBlockUpdateEvent WorldBlockUpdateEvent { get; private set; }
        public static IBlockOpenableInventoryUpdateEvent BlockOpenableInventoryUpdateEvent { get; private set; }
        
        public ServerContext(IItemConfig itemConfig, IBlockConfig blockConfig, ICraftingConfig craftingConfig, IMachineRecipeConfig machineRecipeConfig, IItemStackFactory itemStackFactory, IBlockFactory blockFactory, IWorldBlockDatastore worldBlockDatastore, IWorldBlockUpdateEvent worldBlockUpdateEvent, IBlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent)
        {
            ItemConfig = itemConfig;
            BlockConfig = blockConfig;
            CraftingConfig = craftingConfig;
            MachineRecipeConfig = machineRecipeConfig;
            IItemStackFactory = itemStackFactory;
            BlockFactory = blockFactory;
            WorldBlockDatastore = worldBlockDatastore;
            WorldBlockUpdateEvent = worldBlockUpdateEvent;
            BlockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
        }
    }
}
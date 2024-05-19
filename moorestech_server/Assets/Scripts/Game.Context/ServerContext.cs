using Core.Item.Interface;
using Core.Item.Interface.Config;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Event;
using Game.Block.Interface.RecipeConfig;
using Game.Crafting.Interface;
using Game.Gear.Common;
using Game.Map.Interface.Config;
using Game.Map.Interface.Vein;
using Game.World.Interface.DataStore;

namespace Game.Context
{
    public class ServerContext
    {
        public static IItemConfig ItemConfig { get; private set; }
        public static IBlockConfig BlockConfig { get; private set; }
        public static ICraftingConfig CraftingConfig { get; private set; }
        public static IMachineRecipeConfig MachineRecipeConfig { get; private set; } //TODO これをブロックコンフィグに統合する
        public static IMapObjectConfig MapObjectConfig { get; private set; }

        public static IItemStackFactory ItemStackFactory { get; private set; }
        public static IBlockFactory BlockFactory { get; private set; }

        public static IWorldBlockDatastore WorldBlockDatastore { get; private set; }
        public static IMapVeinDatastore MapVeinDatastore { get; private set; }

        public static IWorldBlockUpdateEvent WorldBlockUpdateEvent { get; private set; }
        public static IBlockOpenableInventoryUpdateEvent BlockOpenableInventoryUpdateEvent { get; private set; }

        public ServerContext(
            IItemConfig itemConfig,
            IBlockConfig blockConfig,
            ICraftingConfig craftingConfig,
            IMachineRecipeConfig machineRecipeConfig,
            IMapObjectConfig mapObjectConfig,
            IItemStackFactory itemStackFactory,
            IBlockFactory blockFactory,
            IWorldBlockDatastore worldBlockDatastore,
            IWorldBlockUpdateEvent worldBlockUpdateEvent,
            IBlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent,
            IMapVeinDatastore mapVeinDatastore)
        {
            ItemConfig = itemConfig;
            BlockConfig = blockConfig;
            CraftingConfig = craftingConfig;
            MachineRecipeConfig = machineRecipeConfig;
            MapObjectConfig = mapObjectConfig;

            ItemStackFactory = itemStackFactory;
            BlockFactory = blockFactory;
            WorldBlockDatastore = worldBlockDatastore;
            MapVeinDatastore = mapVeinDatastore;
            WorldBlockUpdateEvent = worldBlockUpdateEvent;
            BlockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
        }
    }
}
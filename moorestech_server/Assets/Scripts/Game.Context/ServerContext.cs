using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Event;
using Game.Block.Interface.RecipeConfig;
using Game.Challenge;
using Game.Crafting.Interface;
using Game.Map.Interface.Config;
using Game.Map.Interface.Vein;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Context
{
    public class ServerContext
    {
        private static ServiceProvider _serviceProvider;
        
        public static IBlockConfig BlockConfig { get; private set; }
        public static ICraftingConfig CraftingConfig { get; private set; }
        public static IMachineRecipeConfig MachineRecipeConfig { get; private set; } //TODO これをブロックコンフィグに統合する
        public static IMapObjectConfig MapObjectConfig { get; private set; }
        public static IChallengeConfig ChallengeConfig { get; private set; }
        
        public static IItemStackFactory ItemStackFactory { get; private set; }
        public static IBlockFactory BlockFactory { get; private set; }
        
        public static IWorldBlockDatastore WorldBlockDatastore { get; private set; }
        public static IMapVeinDatastore MapVeinDatastore { get; private set; }
        
        public static IWorldBlockUpdateEvent WorldBlockUpdateEvent { get; private set; }
        public static IBlockOpenableInventoryUpdateEvent BlockOpenableInventoryUpdateEvent { get; private set; }
        
        public static TType GetService<TType>()
        {
            return _serviceProvider.GetService<TType>();
        }
        
        public void SetMainServiceProvider(ServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        
        public ServerContext(ServiceProvider initializeServiceProvider)
        {
            ItemConfig = initializeServiceProvider.GetService<IItemConfig>();
            BlockConfig = initializeServiceProvider.GetService<IBlockConfig>();
            CraftingConfig = initializeServiceProvider.GetService<ICraftingConfig>();
            MachineRecipeConfig = initializeServiceProvider.GetService<IMachineRecipeConfig>();
            MapObjectConfig = initializeServiceProvider.GetService<IMapObjectConfig>();
            ChallengeConfig = initializeServiceProvider.GetService<IChallengeConfig>();
            
            ItemStackFactory = initializeServiceProvider.GetService<IItemStackFactory>();
            BlockFactory = initializeServiceProvider.GetService<IBlockFactory>();
            WorldBlockDatastore = initializeServiceProvider.GetService<IWorldBlockDatastore>();
            MapVeinDatastore = initializeServiceProvider.GetService<IMapVeinDatastore>();
            WorldBlockUpdateEvent = initializeServiceProvider.GetService<IWorldBlockUpdateEvent>();
            BlockOpenableInventoryUpdateEvent = initializeServiceProvider.GetService<IBlockOpenableInventoryUpdateEvent>();
        }
    }
}
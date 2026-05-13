using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Map.Interface.MapObject;
using Game.Map.Interface.Vein;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Context
{
    public class ServerContext
    {
        public static bool IsInitialized => _serviceProvider != null;
        private static ServiceProvider _serviceProvider;
        
        public static IItemStackFactory ItemStackFactory { get; private set; }
        public static IBlockFactory BlockFactory { get; private set; }
        
        public static IWorldBlockDatastore WorldBlockDatastore { get; private set; }
        public static IItemMapVeinDatastore ItemMapVeinDatastore { get; private set; }
        public static IFluidMapVeinDatastore FluidMapVeinDatastore { get; private set; }
        public static IMapObjectDatastore MapObjectDatastore { get; private set; }
        
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
            ItemStackFactory = initializeServiceProvider.GetService<IItemStackFactory>();
            BlockFactory = initializeServiceProvider.GetService<IBlockFactory>();
            WorldBlockDatastore = initializeServiceProvider.GetService<IWorldBlockDatastore>();
            ItemMapVeinDatastore = initializeServiceProvider.GetService<IItemMapVeinDatastore>();
            FluidMapVeinDatastore = initializeServiceProvider.GetService<IFluidMapVeinDatastore>();
            WorldBlockUpdateEvent = initializeServiceProvider.GetService<IWorldBlockUpdateEvent>();
            BlockOpenableInventoryUpdateEvent = initializeServiceProvider.GetService<IBlockOpenableInventoryUpdateEvent>();
            MapObjectDatastore = initializeServiceProvider.GetService<IMapObjectDatastore>();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using Server.Event;
using Server.PacketHandle;
using World;
using World.Event;

namespace Server
{
    public static class StartServer
    {
        public static void Main(string[] args)
        {
            
            var services = new ServiceCollection();
            services.AddSingleton<BlockPlaceEvent,BlockPlaceEvent>();
            services.AddSingleton<EventProtocolProvider,EventProtocolProvider>();
            services.AddSingleton<RegisterSendClientEvents,RegisterSendClientEvents>();
            services.AddSingleton<WorldBlockDatastore,WorldBlockDatastore>();
            services.AddSingleton<PlayerInventoryDataStore,PlayerInventoryDataStore>();
            var serviceProvider = services.BuildServiceProvider();
            var packetResponseCreator = new PacketResponseCreator(serviceProvider);
            
            
            new PacketHandler().StartServer(packetResponseCreator);
        }
    }
}
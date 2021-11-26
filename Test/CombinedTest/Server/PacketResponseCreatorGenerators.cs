using System;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using Server.Event;
using Server.Event.EventReceive.EventRegister;
using Server.PacketHandle;
using World;
using World.Event;

namespace Test.CombinedTest.Server
{
    internal static class PacketResponseCreatorGenerators
    {
        public static (PacketResponseCreator,ServiceProvider) Create()
        {
            
            var services = new ServiceCollection();
            services.AddSingleton<BlockPlaceEvent,BlockPlaceEvent>();
            services.AddSingleton<EventProtocolProvider,EventProtocolProvider>();
            services.AddSingleton<RegisterSendClientEvents,RegisterSendClientEvents>();
            services.AddSingleton<WorldBlockDatastore,WorldBlockDatastore>();
            services.AddSingleton<PlayerInventoryDataStore,PlayerInventoryDataStore>();
            var serviceProvider = services.BuildServiceProvider();
            var packetResponse = new PacketResponseCreator(serviceProvider);
            
            return (packetResponse,serviceProvider);
        }
    }
}
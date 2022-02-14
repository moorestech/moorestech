using System;
using System.Threading;
using Core.Item;
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
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            PacketHandler packetHandler = null;

            new Thread(() =>
            {
                packetHandler = new PacketHandler();
                packetHandler.StartServer(packet);
            }).Start();
        }
    }
}
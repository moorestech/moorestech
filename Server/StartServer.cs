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
            var (packet,_) = PacketResponseCreatorDiContainerGenerators.Create();
            new PacketHandler().StartServer(packet);
        }
    }
}
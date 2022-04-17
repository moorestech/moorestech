using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Server.PacketTest
{
    public class InventoryItemMoveProtocolTest
    {
        [Test]
        public void MainInventoryMoveTest()
        {
            //TODO 
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var equipmentInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).EquipmentInventory;
        }

        private byte[] GetPacket()
        {
            
        }
        
        
    }
}
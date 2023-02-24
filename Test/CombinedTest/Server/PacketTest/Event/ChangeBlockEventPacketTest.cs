using Core.Block.BlockFactory;
using Core.Item;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class ChangeBlockEventPacketTest
    {
        [Test]
        public void ChangeBlockEvent()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory); 
            
            //機械のブロックを作る
            var machine = serviceProvider.GetService<BlockFactory>().Create(UnitTestModBlockId.MachineId, 1);
            //機械のブロックを配置
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(machine, 0, 0, BlockDirection.North);
            //機械ブロックにアイテムを挿入するのでそのアイテムを作る
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var item1 = itemStackFactory.
            machine
        }
    }
    
    public class TestMachine
}
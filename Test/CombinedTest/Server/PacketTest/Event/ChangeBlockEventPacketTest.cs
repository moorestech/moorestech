#if NET6_0
using Core.Item;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class ChangeBlockEventPacketTest
    {
        [Test]
        public void MachineChangeStateEvent()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            
            var machine = (VanillaMachineBase)serviceProvider.GetService<IBlockFactory>().Create(UnitTestModBlockId.MachineId, 1);
            
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(machine, 0, 0, BlockDirection.North);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var item1 = itemStackFactory.Create("Test Author:forUniTest", "Test1", 3);
            var item2 = itemStackFactory.Create("Test Author:forUniTest", "Test2", 1);

            machine.InsertItem(item1);
            machine.InsertItem(item2);

            
            machine.SupplyEnergy(100);


            
            packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));

            
            GameUpdater.Update();


            
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var changeStateData = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(response[0].ToArray());

            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, changeStateData.PreviousState);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, changeStateData.CurrentState);
            Assert.AreEqual(0, changeStateData.Position.X);
            Assert.AreEqual(0, changeStateData.Position.Y);

            var detailChangeData = changeStateData.GetStateDat<CommonMachineBlockStateChangeData>();
            Assert.AreEqual(1.0f, detailChangeData.PowerRate);
        }
    }
}
#endif
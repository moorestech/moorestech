using Core.Block.BlockFactory;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Item;
using Core.Update;
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
            
            //機械のブロックを作る
            var machine = (VanillaMachine)serviceProvider.GetService<BlockFactory>().Create(UnitTestModBlockId.MachineId, 1);
            //機械のブロックを配置
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(machine, 0, 0, BlockDirection.North);
            //機械ブロックにアイテムを挿入するのでそのアイテムを挿入する
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var item1 = itemStackFactory.Create("Test Author:forUniTest", "Test1", 2);
            var item2 = itemStackFactory.Create("Test Author:forUniTest", "Test2", 2);

            machine.InsertItem(item1);
            machine.InsertItem(item2);

            //稼働用の電気を供給する
            machine.SupplyPower(1000);
            //アップデートしてステートを更新する
            GameUpdate.Update();

            
            //ステートが実行中になっているかをチェック
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var changeStateData = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(response[0].ToArray());
            
            Assert.AreEqual(VanillaMachineBlockStateController.IdleState,changeStateData.PreviousState);
            Assert.AreEqual(VanillaMachineBlockStateController.ProcessingState,changeStateData.CurrentState);
            Assert.AreEqual(0,changeStateData.Position.X);
            Assert.AreEqual(0,changeStateData.Position.Y);
            
            
            //電力の供給を切る
            machine.SupplyPower(0);
            //アップデートしてステートを更新する
            GameUpdate.Update();
            
            //ステートが停止になっているかをチェック
            response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            changeStateData = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(response[0].ToArray());
            
            Assert.AreEqual(VanillaMachineBlockStateController.ProcessingState,changeStateData.PreviousState);
            Assert.AreEqual(VanillaMachineBlockStateController.IdleState,changeStateData.CurrentState);
            Assert.AreEqual(0,changeStateData.Position.X);
            Assert.AreEqual(0,changeStateData.Position.Y);
        }
    }
}
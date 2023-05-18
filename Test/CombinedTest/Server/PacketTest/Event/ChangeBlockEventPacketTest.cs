using System;
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
            
            var item1 = itemStackFactory.Create("Test Author:forUniTest", "Test1", 3);
            var item2 = itemStackFactory.Create("Test Author:forUniTest", "Test2", 1);

            machine.InsertItem(item1);
            machine.InsertItem(item2);

            //稼働用の電気を供給する
            machine.SupplyPower(100);
            
            
            //最初にイベントをリクエストして、ブロードキャストを受け取れるようにする
            packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            
            //アップデートしてステートを更新する
            GameUpdate.Update();

            
            //ステートが実行中になっているかをチェック
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var changeStateData = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(response[0].ToArray());
            
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState,changeStateData.PreviousState);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState,changeStateData.CurrentState);
            Assert.AreEqual(0,changeStateData.Position.X);
            Assert.AreEqual(0,changeStateData.Position.Y);

            var detailChangeData = changeStateData.GetStateDat<ChangeMachineBlockStateChangeData>();
            Assert.AreEqual(1.0f,detailChangeData.PowerRate);

        }
    }
}
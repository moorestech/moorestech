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
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ChangeBlockEventPacketTest
    {
        [Test]
        public void MachineChangeStateEvent()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            //機械のブロックを作る
            var machine = (VanillaMachineBase)serviceProvider.GetService<IBlockFactory>().Create(UnitTestModBlockId.MachineId, 1);
            //機械のブロックを配置
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(machine, 0, 0, BlockDirection.North);
            //機械ブロックにアイテムを挿入するのでそのアイテムを挿入する
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var item1 = itemStackFactory.Create("Test Author:forUniTest", "Test1", 3);
            var item2 = itemStackFactory.Create("Test Author:forUniTest", "Test2", 1);

            machine.InsertItem(item1);
            machine.InsertItem(item2);

            //稼働用の電気を供給する
            machine.SupplyEnergy(100);


            //最初にイベントをリクエストして、ブロードキャストを受け取れるようにする
            packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));

            //アップデートしてステートを更新する
            GameUpdater.UpdateWithWait();


            //ステートが実行中になっているかをチェック
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var payLoad = eventMessagePack.Events[0].Payload;
            
            var changeStateData = MessagePackSerializer.Deserialize<ChangeBlockStateEventMessagePack>(payLoad);

            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, changeStateData.PreviousState);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, changeStateData.CurrentState);
            Assert.AreEqual(0, changeStateData.Position.X);
            Assert.AreEqual(0, changeStateData.Position.Y);

            var detailChangeData = changeStateData.GetStateDat<CommonMachineBlockStateChangeData>();
            Assert.AreEqual(1.0f, detailChangeData.powerRate);
        }
    }
}
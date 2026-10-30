using System.Linq;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.SpaceElevator;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorEventRecoveryTest
    {
        [Test]
        public void RealEventsFanOutArrivalConnectionRefundAndInitialPull()
        {
            var (_, provider) = CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(provider, 1);
            var first = PlaceBase(Vector3Int.zero);
            var secondPos = new Vector3Int(10, 0, 0);
            PlaceBase(secondPos);
            sink.TakeAll();
            var service = ServerContext.GetService<SpaceElevatorConnectionService>();

            // 二基の受信ペイロードを捕捉して全拠点配信を守る
            // Capture received payloads for two bases to protect fanout
            LaunchCables(150);
            AssertPool(150, Vector3Int.zero);
            AssertPool(150, secondPos);
            sink.TakeAll();
            service.Connect(first);
            AssertPool(50, Vector3Int.zero);
            AssertPool(50, secondPos);
            Assert.IsTrue(Last(Vector3Int.zero).IsConnected);
            sink.TakeAll();
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            AssertPool(150, secondPos);
            sink.TakeAll();

            // 設置後の初期pullは同値でも必ずイベントを返す
            // Initial pull after placement must emit an event even for unchanged values
            var protocol = new RequestBlockStateProtocol(provider);
            var request = new RequestBlockStateProtocol.RequestBlockStateProtocolMessagePack(secondPos);
            protocol.GetResponse(MessagePackSerializer.Serialize(request), 1);
            AssertPool(150, secondPos);
            sink.TakeAll();
            protocol.GetResponse(MessagePackSerializer.Serialize(request), 1);
            AssertPool(150, secondPos);

            #region Internal
            SpaceElevatorBaseStateDetail Last(Vector3Int pos)
            {
                EventTestUtil.CommitBlockStates();
                var state = EventTestUtil.ExtractBlockStates(sink.Events).Last(item => (Vector3Int)item.Position == pos);
                return state
                    .GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            }
            void AssertPool(int count, Vector3Int pos) { Assert.AreEqual(count, Last(pos).CablePoolAmount); }
            #endregion
        }

        [Test]
        public void RealEventsCarryProgressOutletWaitResumeAndCompletion()
        {
            var (_, provider) = CreateServer();
            var block = PlaceBase(Vector3Int.zero);
            PowerBase(block, Vector3Int.zero, 100f);
            LaunchCables(100);
            var sink = EventTestUtil.RegisterCaptureSink(provider, 1);
            ServerContext.GetService<SpaceElevatorConnectionService>().Connect(block);
            Assert.IsTrue(Last().IsConnected);
            var inventory = block.GetComponent<SpaceElevatorBaseInventoryComponent>();
            inventory.SetItem(0, ForUnitTestItemId.TestElevatorSatellite, 1);
            RunSeconds(0.5);
            Assert.AreEqual(SpaceElevatorBaseStatus.Climbing, Last().Status);
            Assert.That(Last().ClimbProgress, Is.GreaterThan(0).And.LessThan(1));
            sink.TakeAll();
            inventory.SetItem(1, ForUnitTestItemId.ItemId1, 1);
            RunSeconds(3);
            Assert.AreEqual(SpaceElevatorBaseStatus.OutputBlocked, Last().Status);
            Assert.AreEqual(1f, Last().ClimbProgress);
            sink.TakeAll();
            inventory.SetItem(1, ServerContext.ItemStackFactory.CreatEmpty());
            RunSeconds(0.1);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, Last().Status);
            Assert.AreEqual(0f, Last().ClimbProgress);
            Assert.AreEqual(5, inventory.GetItem(1).Count);

            #region Internal
            SpaceElevatorBaseStateDetail Last()
            {
                EventTestUtil.CommitBlockStates();
                var state = EventTestUtil.ExtractBlockStates(sink.Events).Last(item => (Vector3Int)item.Position == Vector3Int.zero);
                return state
                    .GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey);
            }
            #endregion
        }
    }
}

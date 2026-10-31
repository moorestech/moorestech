using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.SpaceElevator;
using NUnit.Framework;
using Game.Block.Interface.Extension;
using UnityEngine;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorConnectionTest
    {
        [Test]
        public void ユーザー原文の手順で接続と撤去の返却が働く()
        {
            // 1エレベーター100本。200本打ち上げて2基接続、50本足しても3基目は接続できない
            // 100 per elevator: launch 200 and connect two; adding 50 still cannot connect a third
            var (_, provider) = CreateServer();
            var service = ServerContext.GetService<SpaceElevatorConnectionService>();
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            var first = PlaceBase(new Vector3Int(0, 0, 0));
            var second = PlaceBase(new Vector3Int(10, 0, 0));
            var third = PlaceBase(new Vector3Int(20, 0, 0));
            LaunchCables(200);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(first));
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(second));
            LaunchCables(50);
            Assert.AreEqual(SpaceElevatorConnectResult.InsufficientCable, service.Connect(third));
            Assert.AreEqual(50, pool.Amount);

            // 接続済みの1基を撤去すると150になり、3基目が接続できる
            // Removing one connected base makes 150, and the third can connect
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(10, 0, 0), BlockRemoveReason.ManualRemove);
            Assert.AreEqual(150, pool.Amount);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(third));
            Assert.AreEqual(50, pool.Amount);
        }

        [Test]
        public void 未接続の拠点の撤去はプールに影響しない()
        {
            CreateServer();
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            Assert.AreEqual(100, pool.Amount);
        }

        [Test]
        public void ちょうど1エレベーター分で接続でき二度目は接続済みを返す()
        {
            CreateServer();
            var service = ServerContext.GetService<SpaceElevatorConnectionService>();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            LaunchCables(100);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(elevatorBase));
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, service.Connect(elevatorBase));
            Assert.AreEqual(0, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
        }

        [Test]
        public void プールの増減が全拠点の状態に届く()
        {
            CreateServer();
            var elevatorBase = PlaceBase(Vector3Int.zero);
            Assert.AreEqual(SpaceElevatorBaseStatus.InsufficientCable, ReadDetail(elevatorBase).Status);

            // テストmodの1エレベーター分は100。残量が届き、足りると接続できる状態に変わる
            // The test mod needs 100 per elevator; the amount arrives and the state turns connectable once enough remains
            LaunchCables(150);
            var detail = ReadDetail(elevatorBase);
            Assert.AreEqual(150, detail.CablePoolAmount);
            Assert.AreEqual(SpaceElevatorBaseStatus.ReadyToConnect, detail.Status);
        }

        public static SpaceElevatorBaseStateDetail ReadDetail(IBlock elevatorBase)
        {
            var details = elevatorBase.GetComponent<SpaceElevatorClimberComponent>().GetBlockStateDetails();
            return MessagePack.MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(details[0].Value);
        }
    }
}

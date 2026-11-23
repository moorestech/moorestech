using System.Text.RegularExpressions;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.SpaceElevator;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.OrbitalCargo;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.OrbitalCargo
{
    // 選んでから接続する順をサーバーが守る（ADR 0106 7）
    // The server enforces selecting before connecting (ADR 0106 7)
    public class OrbitalCargoBaseConnectTest
    {
        [Test]
        public void 未選択の拠点は接続を拒否しケーブルを減らさない()
        {
            CreateServer();
            var elevatorBase = PlaceUnselectedBase(Vector3Int.zero);
            LaunchCables(100);
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            var service = ServerContext.GetService<SpaceElevatorConnectionService>();
            Assert.AreEqual(SpaceElevatorBaseStatus.CargoNotSelected, ReadDetail(elevatorBase).Status);

            // 拒否は消費の前で、理由をログに残す
            // The refusal comes before consuming and leaves its reason in the log
            LogAssert.Expect(LogType.Log, new Regex("connection denied: CargoNotSelected"));
            Assert.AreEqual(SpaceElevatorConnectResult.CargoNotSelected, service.Connect(elevatorBase));
            Assert.AreEqual(100, pool.Amount);
            Assert.IsFalse(elevatorBase.GetComponent<ISpaceElevatorBase>().IsConnected);

            // 選べば接続できる表示になり、同じ要求が通る
            // After selecting, the base shows it can connect and the same request succeeds
            OrbitalCargoTestUtil.SelectCargo(elevatorBase, ForUnitTestItemId.TestElevatorSatellite);
            Assert.AreEqual(SpaceElevatorBaseStatus.ReadyToConnect, ReadDetail(elevatorBase).Status);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, service.Connect(elevatorBase));
            Assert.AreEqual(0, pool.Amount);
        }

        [Test]
        public void 接続済みの判定は未選択の判定より先に返る()
        {
            CreateServer();
            var elevatorBase = PlaceUnselectedBase(Vector3Int.zero);
            elevatorBase.GetComponent<ISpaceElevatorBase>().MarkConnected();
            LogAssert.Expect(LogType.Log, new Regex("connection denied: AlreadyConnected"));
            Assert.AreEqual(SpaceElevatorConnectResult.AlreadyConnected, ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase));
        }
    }
}

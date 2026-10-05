using Server.Protocol.PacketResponse.Rail;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Train.SaveLoad;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest.Rail
{
    public class RailConnectByDestinationProtocolTest : RailConnectByDestinationTestBase
    {
        [Test]
        public void 座標で同定したノード同士を接続し素材を消費する()
        {
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5);

            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination, ConnectToolGuid);

            Assert.AreEqual(0, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(0, CountItem(_ironPlateId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, _toNode, "fromNode", "toNode");
            AssertNotification(null);
        }

        [Test]
        public void 再設置でノードIdとGuidが変わっても座標から解決して接続できる()
        {
            // 終点の橋脚を撤去・再設置し、旧Guidが無効になった状態を作る
            // Remove and re-place the target pier so the old guid becomes invalid
            var destination = _toNode.ConnectionDestination;
            var oldGuid = _toNode.Guid;
            _environment.WorldBlockDatastore.RemoveBlock(ToRailPosition, BlockRemoveReason.ManualRemove);
            var newToNode = TrainTestHelper.PlaceRail(_environment, ToRailPosition, BlockDirection.North).BackNode;
            Assert.AreNotEqual(oldGuid, newToNode.Guid);

            var units = CalculateUnits(_fromNode, newToNode);
            SetInventory(units * 12, units * 5);
            Send(_fromNode.ConnectionDestination, destination, ConnectToolGuid);

            Assert.AreEqual(0, CountItem(_reinforcingMaterialId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, newToNode, "fromNode", "newToNode");
        }

        [Test]
        public void 端点が無い座標は接続せず何も消費しない()
        {
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5);
            var missing = new ConnectionDestination(new Vector3Int(99, 0, 99), 0, false);

            LogAssert.Expect(LogType.Warning, new Regex(@"\[RailConnectByDestination\] endpoint not found\."));
            LogAssert.Expect(LogType.Warning, "[RailConnectByDestination] connection denied: InvalidNode");
            Send(_fromNode.ConnectionDestination, missing, ConnectToolGuid);
            AssertNotification("denied.railEdit.InvalidNode");

            Assert.IsFalse(_fromNode.ConnectedNodes.Any());
            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5, CountItem(_ironPlateId));
        }

        [Test]
        public void 素材不足なら接続せず何も消費しない()
        {
            // 既存の接続判定（EvaluatePlacement）をそのまま通っていることを確かめる
            // Verify the existing connect judgement (EvaluatePlacement) is applied as-is
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5 - 1);

            LogAssert.Expect(LogType.Warning, "[RailConnectByDestination] connection denied: NotEnoughRailItem");
            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination, ConnectToolGuid);
            AssertNotification("denied.railEdit.NotEnoughRailItem");

            Assert.IsFalse(_fromNode.ConnectedNodes.Any());
            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5 - 1, CountItem(_ironPlateId));
        }

        [Test]
        public void 既に接続済みなら二重に消費しない()
        {
            // 2本分の素材を持たせて2回送り、2回目は消費しないことを確かめる
            // Hold materials for two rails, send twice, and verify the second send consumes nothing
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 24, units * 10);

            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination, ConnectToolGuid);
            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination, ConnectToolGuid);

            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5, CountItem(_ironPlateId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, _toNode, "fromNode", "toNode");
            AssertNotification(null);
        }

        [TestCase("00000000-0000-0000-0000-000000000000")]
        [TestCase("ffffffff-ffff-ffff-ffff-ffffffffffff")]
        public void 未解放や未指定の種類は接続せず通知する(string toolGuid)
        {
            // 素材が足りても既存の解放判定を迂回しない
            // Sufficient materials must not bypass the existing unlock check
            var units = CalculateUnits(_fromNode, _toNode);
            SetInventory(units * 12, units * 5);
            LogAssert.Expect(LogType.Warning, "[RailConnectByDestination] connection denied: NotUnlocked");
            Send(_fromNode.ConnectionDestination, _toNode.ConnectionDestination, Guid.Parse(toolGuid));

            Assert.IsFalse(_fromNode.ConnectedNodes.Any());
            Assert.AreEqual(units * 12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(units * 5, CountItem(_ironPlateId));
            AssertNotification("denied.railEdit.NotUnlocked");
        }
    }
}

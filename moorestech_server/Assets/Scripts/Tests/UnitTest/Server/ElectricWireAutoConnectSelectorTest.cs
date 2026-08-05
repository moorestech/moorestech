using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// 選定コアの純粋単体テスト。ワールド状態非参照
    /// SetUpはマスタ実値取得のみ目的
    /// Pure unit tests for the selection core; never touches world state
    /// SetUp builds the DI container only to fetch real master values
    /// </summary>
    public class ElectricWireAutoConnectSelectorTest
    {
        private ElectricPoleBlockParam _poleParam;
        private IBlockParam _machineParam;

        [SetUp]
        public void SetUp()
        {
            // マスタ含むサーバーコンテキスト構築
            // Build server context with master data
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _poleParam = (ElectricPoleBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPoleId).BlockParam;
            _machineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockParam;
        }

        private static BlockPositionInfo Cell(int x, int y, int z)
        {
            return new BlockPositionInfo(new Vector3Int(x, y, z), BlockDirection.North, Vector3Int.one);
        }

        private ElectricWireConnectCandidate Pole(int id, int x, int connectionCount)
        {
            return new ElectricWireConnectCandidate(new BlockInstanceId(id), _poleParam, Cell(x, 0, 0), connectionCount);
        }

        private ElectricWireConnectCandidate Machine(int id, int x, int connectionCount)
        {
            return new ElectricWireConnectCandidate(new BlockInstanceId(id), _machineParam, Cell(x, 0, 0), connectionCount);
        }

        [Test]
        public void 電柱設置は最寄り電柱1本と未接続機械を距離順に選ぶ()
        {
            // 電柱d3+機械d1,d2。結果は電柱→機械順
            // Pole(d3)+2 machines(d1,d2); pole then machines by distance
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 0), Machine(20, 2, 0), Machine(21, -1, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
            Assert.AreEqual(new BlockInstanceId(21), result[1].TargetId);
            Assert.AreEqual(new BlockInstanceId(20), result[2].TargetId);
        }

        [Test]
        public void 同距離の電柱はInstanceId昇順で選ばれる()
        {
            // X±3同距離、ID小11が優先
            // X=+3/-3 tie at distance 3; lower id 11 wins
            var candidates = new List<ElectricWireConnectCandidate> { Pole(12, 3, 0), Pole(11, -3, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(11), result[0].TargetId);
        }

        [Test]
        public void 接続済みの機械は選ばれない()
        {
            var candidates = new List<ElectricWireConnectCandidate> { Machine(20, 2, 1) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void 容量満杯の電柱は候補から除外される()
        {
            // 電柱上限8に達している電柱は接続不可
            // A pole already at its capacity of 8 is not connectable
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 8) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void 残容量1の電柱は選ばれる()
        {
            // 電柱上限8中7本使用でも残1本で接続可
            // A pole with 7 of 8 connections used still has one slot left and is selectable
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 7) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
        }

        [Test]
        public void 同距離の機械はInstanceId昇順で選ばれる()
        {
            // X±2同距離、列挙は21先だがID順20先
            // X=+2 and X=-2 tie at distance 2; machine 21 is enumerated first but id-order puts 20 first
            var candidates = new List<ElectricWireConnectCandidate> { Machine(21, 2, 0), Machine(20, -2, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(new BlockInstanceId(20), result[0].TargetId);
            Assert.AreEqual(new BlockInstanceId(21), result[1].TargetId);
        }

        [Test]
        public void 機械設置は最寄り電柱1本のみを選ぶ()
        {
            // 電柱対機械範囲5(±2)内、他機械対象外
            // Within pole's machine range 5(±2); others excluded
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 2, 0), Machine(20, 1, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_machineParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
        }

        [Test]
        public void 相互範囲外の電柱は選ばれない()
        {
            // 対機械範囲5(±2)に対しX差3は範囲外
            // X distance 3 exceeds pole's machine range 5(±2)
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPlacementTargets(_machineParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }
    }
}

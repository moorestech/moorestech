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
    /// 自動接続候補選定コアの純粋単体テスト。ワールド状態には依存しない
    /// Pure unit tests for the auto-connect selection core; no world state involved
    /// </summary>
    public class ElectricWireAutoConnectSelectorTest
    {
        private ElectricPoleBlockParam _poleParam;
        private IBlockParam _machineParam;

        [SetUp]
        public void SetUp()
        {
            // マスタデータを含むサーバーコンテキストを構築する
            // Build server context including master data
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
            // 電柱(d3)＋機械2台(d1, d2)。結果は電柱→機械を距離順
            // One pole (d3) and two machines (d1, d2); expect pole first then machines by distance
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 0), Machine(20, 2, 0), Machine(21, -1, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
            Assert.AreEqual(new BlockInstanceId(21), result[1].TargetId);
            Assert.AreEqual(new BlockInstanceId(20), result[2].TargetId);
        }

        [Test]
        public void 同距離の電柱はInstanceId昇順で選ばれる()
        {
            // X=+3とX=-3は同距離3。ID小の11が最寄り扱いになる
            // X=+3 and X=-3 tie at distance 3; the lower id 11 wins
            var candidates = new List<ElectricWireConnectCandidate> { Pole(12, 3, 0), Pole(11, -3, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(11), result[0].TargetId);
        }

        [Test]
        public void 接続済みの機械は選ばれない()
        {
            var candidates = new List<ElectricWireConnectCandidate> { Machine(20, 2, 1) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void 容量満杯の電柱は候補から除外される()
        {
            // 電柱上限8に達している電柱は接続不可
            // A pole already at its capacity of 8 is not connectable
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 8) };

            var result = ElectricWireAutoConnectSelector.SelectPoleTargets(_poleParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void usedCountが残容量から差し引かれる()
        {
            // 上限8のうち7本使用済みなら機械は1台しか選ばれない
            // With 7 of 8 connections used, only one machine is selected
            var candidates = new List<ElectricWireConnectCandidate> { Machine(20, 1, 0), Machine(21, 2, 0) };

            var result = ElectricWireAutoConnectSelector.SelectPoleMachineTargets(_poleParam, Cell(0, 0, 0), 7, candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(20), result[0].TargetId);
        }

        [Test]
        public void 機械設置は最寄り電柱1本のみを選ぶ()
        {
            // 電柱の対機械範囲5(±2)内。他の機械は対象外
            // Within the pole's machine range 5 (±2); other machines are never selected
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 2, 0), Machine(20, 1, 0) };

            var result = ElectricWireAutoConnectSelector.SelectMachineTargets(_machineParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new BlockInstanceId(10), result[0].TargetId);
        }

        [Test]
        public void 相互範囲外の電柱は選ばれない()
        {
            // 電柱の対機械範囲5(±2)に対しX差3は範囲外
            // X distance 3 exceeds the pole's machine range 5 (±2)
            var candidates = new List<ElectricWireConnectCandidate> { Pole(10, 3, 0) };

            var result = ElectricWireAutoConnectSelector.SelectMachineTargets(_machineParam, Cell(0, 0, 0), candidates);

            Assert.AreEqual(0, result.Count);
        }
    }
}

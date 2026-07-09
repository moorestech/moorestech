using System;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.CombinedTest.Server.PacketTest.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GearChainPoleExtendProtocolTest
    {
        private static readonly Vector3Int FromPos = GearChainPoleExtendTestHelper.FromPos;

        private GearChainPoleExtendTestHelper _helper;

        [SetUp]
        public void SetUp()
        {
            _helper = new GearChainPoleExtendTestHelper();
        }

        [Test]
        public void ExtendPlacesConnectsAndConsumesItems()
        {
            _helper.SetInventory(5, 3);
            var placePos = new Vector3Int(3, 0, 0);
            var response = _helper.SendExtend(placePos);

            // 設置・応答内容を検証する
            // Verify placement and response payload
            Assert.True(response.IsSuccess);
            Assert.True(ServerContext.WorldBlockDatastore.Exists(placePos));
            ServerContext.WorldBlockDatastore.TryGetBlock(placePos, out var placedBlock);
            Assert.AreEqual(placedBlock.BlockInstanceId.AsPrimitive(), response.PlacedBlockInstanceId);
            Assert.AreEqual(placePos, (Vector3Int)response.PlacedPolePos);

            // 双方向のチェーン接続を検証する
            // Verify bidirectional chain connection
            GearChainSystemUtil.TryGetGearChainPole(FromPos, out var fromPole, out var fromTransformer);
            GearChainSystemUtil.TryGetGearChainPole(placePos, out var newPole, out var newTransformer);
            Assert.True(fromPole.ContainsChainConnection(newPole.BlockInstanceId));
            Assert.True(newPole.ContainsChainConnection(fromPole.BlockInstanceId));
            Assert.AreEqual(newTransformer, fromTransformer.GetGearConnects().Single().Transformer);

            // 距離3分のチェーンと建設コスト(Test3 x2)の消費を検証する
            // Verify consumption of 3 chains (distance) and the construction cost (Test3 x2)
            Assert.AreEqual(2, _helper.CountItem(_helper.ChainItemId));
            Assert.AreEqual(1, _helper.CountItem(_helper.MaterialItemId));
        }

        [Test]
        public void IsolatedPlaceDoesNotConnectOrConsumeChain()
        {
            _helper.SetInventory(5, 3);
            var placePos = new Vector3Int(5, 0, 5);
            var response = _helper.SendIsolated(placePos);

            // 接続なしで設置されチェーン非消費・素材のみ消費を検証
            // Verify pole placed without connection: no chain consumed, only materials consumed
            Assert.True(response.IsSuccess);
            Assert.True(ServerContext.WorldBlockDatastore.Exists(placePos));
            GearChainSystemUtil.TryGetGearChainPole(placePos, out _, out var transformer);
            Assert.IsEmpty(transformer.GetGearConnects());
            Assert.AreEqual(5, _helper.CountItem(_helper.ChainItemId));
            Assert.AreEqual(1, _helper.CountItem(_helper.MaterialItemId));
        }

        [Test]
        public void ExtendFailsWhenTooFar()
        {
            _helper.SetInventory(20, 3);
            _helper.AssertExtendFailsWithoutStateChange(new Vector3Int(11, 0, 0), GearChainPlacementEvaluator.TooFarError, 20, 3);
        }

        [Test]
        public void ExtendFailsWithoutChainItem()
        {
            _helper.SetInventory(2, 3);
            _helper.AssertExtendFailsWithoutStateChange(new Vector3Int(3, 0, 0), GearChainPlacementEvaluator.NoItemError, 2, 3);
        }

        [Test]
        public void ExtendFailsWithoutMaterial()
        {
            // 素材(建設コスト)を持たない場合はInsufficientItemsで失敗し状態不変
            // Without the materials (construction cost), it fails with InsufficientItems and leaves no state change
            _helper.SetInventory(5, 0);
            _helper.AssertExtendFailsWithoutStateChange(new Vector3Int(3, 0, 0), GearChainPlacementEvaluator.InsufficientItemsError, 5, 0);
        }

        [Test]
        public void ExtendFailsWhenNotUnlocked()
        {
            // 未解放ポールブロックIDを送るとNotUnlockedで失敗し状態不変
            // Sending a locked pole BlockId fails with NotUnlocked and leaves no state change
            _helper.SetInventory(5, 3);
            var placePos = new Vector3Int(3, 0, 0);
            var response = _helper.SendExtendWithBlock(placePos, ForUnitTestModBlockId.LockedGearChainPole);

            Assert.False(response.IsSuccess);
            Assert.AreEqual(GearChainPlacementEvaluator.NotUnlockedError, response.Error);
            Assert.False(ServerContext.WorldBlockDatastore.Exists(placePos));
            Assert.AreEqual(5, _helper.CountItem(_helper.ChainItemId));
            Assert.AreEqual(3, _helper.CountItem(_helper.MaterialItemId));
        }

        [Test]
        public void ExtendFailsWhenConnectionLimit()
        {
            // 起点ポールを上限(2)まで接続してから延長を試みる
            // Fill the source pole to its limit (2) then attempt extension
            _helper.SetInventory(10, 3);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(-2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.True(GearChainSystemUtil.TryConnect(FromPos, new Vector3Int(2, 0, 0), GearChainPoleExtendTestHelper.PlayerId, _helper.ChainItemId, out _));
            Assert.True(GearChainSystemUtil.TryConnect(FromPos, new Vector3Int(-2, 0, 0), GearChainPoleExtendTestHelper.PlayerId, _helper.ChainItemId, out _));

            // 手動接続でチェーン4個消費済み(6残)、素材は未消費(3)
            // 4 chains consumed by manual connects (6 left), materials untouched (3)
            _helper.AssertExtendFailsWithoutStateChange(new Vector3Int(0, 0, 3), GearChainPlacementEvaluator.ConnectionLimitError, 6, 3);
        }

        [Test]
        public void ExtendFailsWhenPositionOccupied()
        {
            _helper.SetInventory(5, 3);
            var placePos = new Vector3Int(3, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, placePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var response = _helper.SendExtend(placePos);
            Assert.False(response.IsSuccess);
            Assert.AreEqual(GearChainPlacementEvaluator.PositionOccupiedError, response.Error);
            Assert.AreEqual(5, _helper.CountItem(_helper.ChainItemId));
            Assert.AreEqual(3, _helper.CountItem(_helper.MaterialItemId));
        }
    }
}

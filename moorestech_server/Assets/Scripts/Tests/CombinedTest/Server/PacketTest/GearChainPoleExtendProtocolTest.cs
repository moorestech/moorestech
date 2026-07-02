using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GearChainPoleExtendProtocolTest
    {
        private const int PlayerId = 1;
        private static readonly Vector3Int FromPos = Vector3Int.zero;

        private PacketResponseCreator _packet;
        private ItemId _chainItemId;
        private ItemId _poleItemId;
        private IOpenableInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            // サーバーと起点ポールを準備する
            // Prepare the server and the source pole
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _packet = packet;
            _chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);
            _poleItemId = MasterHolder.BlockMaster.GetItemId(ForUnitTestModBlockId.GearChainPole);
            _inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, FromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        [Test]
        public void ExtendPlacesConnectsAndConsumesItems()
        {
            SetInventory(5, 2);
            var placePos = new Vector3Int(3, 0, 0);
            var response = SendExtend(placePos);

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

            // 距離3分のチェーンとポール1個の消費を検証する
            // Verify consumption of 3 chains (distance) and 1 pole
            Assert.AreEqual(2, CountItem(_chainItemId));
            Assert.AreEqual(1, CountItem(_poleItemId));
        }

        [Test]
        public void IsolatedPlaceDoesNotConnectOrConsumeChain()
        {
            SetInventory(5, 1);
            var placePos = new Vector3Int(5, 0, 5);
            var response = SendIsolated(placePos);

            // 接続なしで設置され、チェーンは消費されないことを検証する
            // Verify pole placed without connection and no chain consumed
            Assert.True(response.IsSuccess);
            Assert.True(ServerContext.WorldBlockDatastore.Exists(placePos));
            GearChainSystemUtil.TryGetGearChainPole(placePos, out _, out var transformer);
            Assert.IsEmpty(transformer.GetGearConnects());
            Assert.AreEqual(5, CountItem(_chainItemId));
            Assert.AreEqual(0, CountItem(_poleItemId));
        }

        [Test]
        public void ExtendFailsWhenTooFar()
        {
            SetInventory(20, 1);
            AssertExtendFailsWithoutStateChange(new Vector3Int(11, 0, 0), GearChainPlacementEvaluator.TooFarError, 20, 1);
        }

        [Test]
        public void ExtendFailsWithoutChainItem()
        {
            SetInventory(2, 1);
            AssertExtendFailsWithoutStateChange(new Vector3Int(3, 0, 0), GearChainPlacementEvaluator.NoItemError, 2, 1);
        }

        [Test]
        public void ExtendFailsWithoutPoleItem()
        {
            SetInventory(5, 0);
            AssertExtendFailsWithoutStateChange(new Vector3Int(3, 0, 0), GearChainPlacementEvaluator.NoPoleItemError, 5, 0);
        }

        [Test]
        public void ExtendFailsWhenConnectionLimit()
        {
            // 起点ポールを上限(2)まで接続してから延長を試みる
            // Fill the source pole to its limit (2) then attempt extension
            SetInventory(10, 1);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(-2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.True(GearChainSystemUtil.TryConnect(FromPos, new Vector3Int(2, 0, 0), PlayerId, _chainItemId, out _));
            Assert.True(GearChainSystemUtil.TryConnect(FromPos, new Vector3Int(-2, 0, 0), PlayerId, _chainItemId, out _));

            AssertExtendFailsWithoutStateChange(new Vector3Int(0, 0, 3), GearChainPlacementEvaluator.ConnectionLimitError, 6, 1);
        }

        [Test]
        public void ExtendFailsWhenPositionOccupied()
        {
            SetInventory(5, 1);
            var placePos = new Vector3Int(3, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, placePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var response = SendExtend(placePos);
            Assert.False(response.IsSuccess);
            Assert.AreEqual("PositionOccupied", response.Error);
            Assert.AreEqual(5, CountItem(_chainItemId));
            Assert.AreEqual(1, CountItem(_poleItemId));
        }

        private void AssertExtendFailsWithoutStateChange(Vector3Int placePos, string expectedError, int expectedChainCount, int expectedPoleCount)
        {
            // 失敗時に一切の状態変更が起きないことを検証する
            // Verify failures leave absolutely no state changes
            var response = SendExtend(placePos);
            Assert.False(response.IsSuccess);
            Assert.AreEqual(expectedError, response.Error);
            Assert.False(ServerContext.WorldBlockDatastore.Exists(placePos));
            Assert.AreEqual(expectedChainCount, CountItem(_chainItemId));
            Assert.AreEqual(expectedPoleCount, CountItem(_poleItemId));
        }

        private void SetInventory(int chainCount, int poleCount)
        {
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, chainCount));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_poleItemId, poleCount));
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendExtend(Vector3Int placePos)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(PlayerId, FromPos, 1, CreatePlaceInfo(placePos), _chainItemId);
            return Send(request);
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendIsolated(Vector3Int placePos)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(PlayerId, 1, CreatePlaceInfo(placePos));
            return Send(request);
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse Send(GearChainPoleExtendProtocol.GearChainPoleExtendRequest request)
        {
            var responseBytes = _packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext()).First();
            return MessagePackSerializer.Deserialize<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(responseBytes.ToArray());
        }

        private static PlaceInfo CreatePlaceInfo(Vector3Int placePos)
        {
            return new PlaceInfo
            {
                Position = placePos,
                Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal,
            };
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}

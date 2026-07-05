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

        // ポールの建設コスト(Test3 x2)。BlockId化に伴いポールアイテムではなく素材を消費する
        // Construction cost of the pole (Test3 x2). After BlockId migration, materials are consumed instead of a pole item
        private static readonly Guid MaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        private PacketResponseCreator _packet;
        private ItemId _chainItemId;
        private ItemId _materialItemId;
        private IOpenableInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            // サーバーと起点ポールを準備する
            // Prepare the server and the source pole
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _packet = packet;
            _chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);
            _materialItemId = MasterHolder.ItemMaster.GetItemId(MaterialGuid);
            _inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, FromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        [Test]
        public void ExtendPlacesConnectsAndConsumesItems()
        {
            SetInventory(5, 3);
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

            // 距離3分のチェーンと建設コスト(Test3 x2)の消費を検証する
            // Verify consumption of 3 chains (distance) and the construction cost (Test3 x2)
            Assert.AreEqual(2, CountItem(_chainItemId));
            Assert.AreEqual(1, CountItem(_materialItemId));
        }

        [Test]
        public void IsolatedPlaceDoesNotConnectOrConsumeChain()
        {
            SetInventory(5, 3);
            var placePos = new Vector3Int(5, 0, 5);
            var response = SendIsolated(placePos);

            // 接続なしで設置されチェーン非消費・素材のみ消費を検証
            // Verify pole placed without connection: no chain consumed, only materials consumed
            Assert.True(response.IsSuccess);
            Assert.True(ServerContext.WorldBlockDatastore.Exists(placePos));
            GearChainSystemUtil.TryGetGearChainPole(placePos, out _, out var transformer);
            Assert.IsEmpty(transformer.GetGearConnects());
            Assert.AreEqual(5, CountItem(_chainItemId));
            Assert.AreEqual(1, CountItem(_materialItemId));
        }

        [Test]
        public void ExtendFailsWhenTooFar()
        {
            SetInventory(20, 3);
            AssertExtendFailsWithoutStateChange(new Vector3Int(11, 0, 0), GearChainPlacementEvaluator.TooFarError, 20, 3);
        }

        [Test]
        public void ExtendFailsWithoutChainItem()
        {
            SetInventory(2, 3);
            AssertExtendFailsWithoutStateChange(new Vector3Int(3, 0, 0), GearChainPlacementEvaluator.NoItemError, 2, 3);
        }

        [Test]
        public void ExtendFailsWithoutMaterial()
        {
            // 素材(建設コスト)を持たない場合はInsufficientItemsで失敗し状態不変
            // Without the materials (construction cost), it fails with InsufficientItems and leaves no state change
            SetInventory(5, 0);
            AssertExtendFailsWithoutStateChange(new Vector3Int(3, 0, 0), GearChainPlacementEvaluator.InsufficientItemsError, 5, 0);
        }

        [Test]
        public void ExtendFailsWhenNotUnlocked()
        {
            // 未解放ポールブロックIDを送るとNotUnlockedで失敗し状態不変
            // Sending a locked pole BlockId fails with NotUnlocked and leaves no state change
            SetInventory(5, 3);
            var placePos = new Vector3Int(3, 0, 0);
            var response = SendExtendWithBlock(placePos, ForUnitTestModBlockId.LockedGearChainPole);

            Assert.False(response.IsSuccess);
            Assert.AreEqual(GearChainPlacementEvaluator.NotUnlockedError, response.Error);
            Assert.False(ServerContext.WorldBlockDatastore.Exists(placePos));
            Assert.AreEqual(5, CountItem(_chainItemId));
            Assert.AreEqual(3, CountItem(_materialItemId));
        }

        [Test]
        public void ExtendFailsWhenConnectionLimit()
        {
            // 起点ポールを上限(2)まで接続してから延長を試みる
            // Fill the source pole to its limit (2) then attempt extension
            SetInventory(10, 3);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(-2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.True(GearChainSystemUtil.TryConnect(FromPos, new Vector3Int(2, 0, 0), PlayerId, _chainItemId, out _));
            Assert.True(GearChainSystemUtil.TryConnect(FromPos, new Vector3Int(-2, 0, 0), PlayerId, _chainItemId, out _));

            // 手動接続でチェーン4個消費済み(6残)、素材は未消費(3)
            // 4 chains consumed by manual connects (6 left), materials untouched (3)
            AssertExtendFailsWithoutStateChange(new Vector3Int(0, 0, 3), GearChainPlacementEvaluator.ConnectionLimitError, 6, 3);
        }

        [Test]
        public void ExtendFailsWhenPositionOccupied()
        {
            SetInventory(5, 3);
            var placePos = new Vector3Int(3, 0, 0);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, placePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var response = SendExtend(placePos);
            Assert.False(response.IsSuccess);
            Assert.AreEqual(GearChainPlacementEvaluator.PositionOccupiedError, response.Error);
            Assert.AreEqual(5, CountItem(_chainItemId));
            Assert.AreEqual(3, CountItem(_materialItemId));
        }

        private void AssertExtendFailsWithoutStateChange(Vector3Int placePos, string expectedError, int expectedChainCount, int expectedMaterialCount)
        {
            // 失敗時に一切の状態変更が起きないことを検証する
            // Verify failures leave absolutely no state changes
            var response = SendExtend(placePos);
            Assert.False(response.IsSuccess);
            Assert.AreEqual(expectedError, response.Error);
            Assert.False(ServerContext.WorldBlockDatastore.Exists(placePos));
            Assert.AreEqual(expectedChainCount, CountItem(_chainItemId));
            Assert.AreEqual(expectedMaterialCount, CountItem(_materialItemId));
        }

        private void SetInventory(int chainCount, int materialCount)
        {
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, chainCount));
            if (0 < materialCount) _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_materialItemId, materialCount));
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendExtend(Vector3Int placePos)
        {
            return SendExtendWithBlock(placePos, ForUnitTestModBlockId.GearChainPole);
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendExtendWithBlock(Vector3Int placePos, BlockId poleBlockId)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(PlayerId, FromPos, poleBlockId, CreatePlaceInfo(placePos), _chainItemId);
            return Send(request);
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendIsolated(Vector3Int placePos)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(PlayerId, ForUnitTestModBlockId.GearChainPole, CreatePlaceInfo(placePos));
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

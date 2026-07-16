using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
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

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    /// <summary>
    /// GearChainPoleExtendProtocolTestのサーバー準備・送受信・不変検証ヘルパー
    /// Server setup, send/receive and no-state-change assertion helper for GearChainPoleExtendProtocolTest
    /// </summary>
    public class GearChainPoleExtendTestHelper
    {
        public const int PlayerId = 1;
        public static readonly Vector3Int FromPos = Vector3Int.zero;

        // ポールの建設コスト(Test3 x2)。BlockId化に伴いポールアイテムではなく素材を消費する
        // Construction cost of the pole (Test3 x2). After BlockId migration, materials are consumed instead of a pole item
        private static readonly Guid MaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        public readonly ItemId ChainItemId;
        public readonly ItemId MaterialItemId;

        private readonly PacketResponseCreator _packet;
        private readonly IOpenableInventory _inventory;

        public GearChainPoleExtendTestHelper()
        {
            // サーバーと起点ポールを準備する
            // Prepare the server and the source pole
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _packet = packet;
            ChainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);
            MaterialItemId = MasterHolder.ItemMaster.GetItemId(MaterialGuid);
            _inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, FromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        public void SetInventory(int chainCount, int materialCount)
        {
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(ChainItemId, chainCount));
            if (0 < materialCount) _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MaterialItemId, materialCount));
        }

        public void AssertExtendFailsWithoutStateChange(Vector3Int placePos, string expectedError, int expectedChainCount, int expectedMaterialCount)
        {
            // 失敗時に一切の状態変更が起きないことを検証する
            // Verify failures leave absolutely no state changes
            var response = SendExtend(placePos);
            Assert.False(response.IsSuccess);
            Assert.AreEqual(expectedError, response.Error);
            Assert.False(ServerContext.WorldBlockDatastore.Exists(placePos));
            Assert.AreEqual(expectedChainCount, CountItem(ChainItemId));
            Assert.AreEqual(expectedMaterialCount, CountItem(MaterialItemId));
        }

        public GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendExtend(Vector3Int placePos)
        {
            return SendExtendWithBlock(placePos, ForUnitTestModBlockId.GearChainPole);
        }

        public GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendExtendWithBlock(Vector3Int placePos, BlockId poleBlockId)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(PlayerId, FromPos, poleBlockId, CreatePlaceInfo(placePos), ChainItemId);
            return Send(request);
        }

        public GearChainPoleExtendProtocol.GearChainPoleExtendResponse SendIsolated(Vector3Int placePos)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(PlayerId, ForUnitTestModBlockId.GearChainPole, CreatePlaceInfo(placePos));
            return Send(request);
        }

        public int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }

        private GearChainPoleExtendProtocol.GearChainPoleExtendResponse Send(GearChainPoleExtendProtocol.GearChainPoleExtendRequest request)
        {
            var responseBytes = _packet.GetPacketResponseForTest(MessagePackSerializer.Serialize(request), new PacketResponseContext()).First();
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
    }
}

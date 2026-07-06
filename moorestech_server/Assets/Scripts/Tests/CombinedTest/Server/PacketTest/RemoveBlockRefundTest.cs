using System;
using System.Collections.Generic;
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
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveBlockRefundTest
    {
        private const int PlayerId = 3;

        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4
        private static readonly Guid TestBlockItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002"); // Test2(旧TestBlockアイテム)

        [Test]
        public void requiredItems定義ブロックの破壊で素材が全額返却される()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.BlockId, new Vector3Int(3, 3), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = GetInventory(serviceProvider);
            packet.GetPacketResponse(CreateRemovePayload(3, 3), new PacketResponseContext());

            // 素材(Test3×2+Test4×1)が返り、旧ブロックアイテム(Test2)は返らない
            // Materials (Test3 x2 + Test4 x1) are refunded; the old block item (Test2) is not
            Assert.IsFalse(world.Exists(new Vector3Int(3, 3)));
            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, GetItemCount(inventory, TestBlockItemGuid));
        }

        [Test]
        public void 長尺ベルトの解体で建設コスト1セットが返却される()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            // 長尺3連ベルトをプロトコル経由で設置（コスト消費と3セル占有を再現）
            // Place the 3-length belt via the protocol to reproduce cost consumption and the 3-cell footprint
            PlaceBlockProtocolTestSupport.GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3, 1);
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(50, 0, 10), Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor3 },
            };
            packet.GetPacketResponse(PlaceBlockProtocolTestSupport.CreatePlacePayload(placeInfos), new PacketResponseContext());

            var inventory = GetInventory(serviceProvider);
            packet.GetPacketResponse(CreateRemovePayload(new Vector3Int(50, 0, 10)), new PacketResponseContext());

            // 1セット分（Test3×1+Test4×1）が返却される
            // One cost set (Test3 x1 + Test4 x1) is refunded
            Assert.IsFalse(world.Exists(new Vector3Int(50, 0, 10)));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void requiredItems未定義ブロックは従来どおりブロックアイテムを返す()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(4, 4), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = GetInventory(serviceProvider);
            packet.GetPacketResponse(CreateRemovePayload(4, 4), new PacketResponseContext());

            // TestBeltConveyorのitemGuidはTest3。従来どおり1個返る
            // TestBeltConveyor's itemGuid is Test3; one item is refunded as before
            Assert.IsFalse(world.Exists(new Vector3Int(4, 4)));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
        }

        #region TestUtil

        private static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        private static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var stack in inventory.InventoryItems)
            {
                if (stack.Id != itemId) continue;
                total += stack.Count;
            }
            return total;
        }

        private static byte[] CreateRemovePayload(int x, int y)
        {
            return CreateRemovePayload(new Vector3Int(x, y));
        }

        private static byte[] CreateRemovePayload(Vector3Int pos)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(PlayerId, pos));
        }

        #endregion
    }
}

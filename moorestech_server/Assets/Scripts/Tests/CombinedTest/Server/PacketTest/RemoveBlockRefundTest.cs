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
            packet.GetPacketResponse(CreateRemovePayload(3, 3), new PacketResponseContext(null));

            // 素材(Test3×2+Test4×1)が返り、旧ブロックアイテム(Test2)は返らない
            // Materials (Test3 x2 + Test4 x1) are refunded; the old block item (Test2) is not
            Assert.IsFalse(world.Exists(new Vector3Int(3, 3)));
            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, GetItemCount(inventory, TestBlockItemGuid));
        }

        [Test]
        public void requiredItems未定義ブロックは何も返却されない()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(4, 4), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = GetInventory(serviceProvider);
            packet.GetPacketResponse(CreateRemovePayload(4, 4), new PacketResponseContext(null));

            // フォールバック廃止により、コスト未定義ブロックは破壊しても本体アイテムを返さない
            // With the fallback removed, destroying a cost-less block refunds no body item
            Assert.IsFalse(world.Exists(new Vector3Int(4, 4)));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
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

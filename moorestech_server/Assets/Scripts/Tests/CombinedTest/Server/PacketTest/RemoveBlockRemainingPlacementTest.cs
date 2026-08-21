using System;
using Core.Master;
using Game.Construction;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 設置数/1セット=3の歯車ベルトで撤去時の凝縮返却（ADR 0026）を検証する
    /// Verifies condensed refund on removal (ADR 0026) with the gear belt whose placementsPerCost is 3
    /// </summary>
    public class RemoveBlockRemainingPlacementTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 三本置いて三本壊すと建設コスト1セットが戻る()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0), (11, 0), (12, 0)), new PacketResponseContext(null));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            Remove(packet, new Vector3Int(10, 0));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, lookup.GetRemainingCount(PlayerId, belt));
            Remove(packet, new Vector3Int(11, 0));
            Assert.AreEqual(2, lookup.GetRemainingCount(PlayerId, belt));
            Remove(packet, new Vector3Int(12, 0));

            // 3本目で設置数/1セットに達し、素材1セットへ凝縮して返る
            // The third removal reaches one set's worth and condenses into one set of materials
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, lookup.GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 一本だけ設置した直後に一本撤去すると建設コスト1セットが戻る()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            // 1本設置で素材1セットを消費し、財布は残り設置数/1セット-1になる
            // Placing one belt consumes one material set, leaving wallet at count-per-set minus one
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(0, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(2, lookup.GetRemainingCount(PlayerId, belt));

            Remove(packet, new Vector3Int(10, 0));

            // 部分消費状態からの1回撤去で財布が設置数/1セットへ到達し、素材が増減なく1セット戻る
            // A single removal from the partially-consumed wallet reaches the per-set count and refunds one set with zero net material change
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, lookup.GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 凝縮返却が入り切らなければ撤去も財布も変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));
            var mutation = serviceProvider.GetService<IRemainingPlacementCountMutation>();
            mutation.TryConsumeOne(PlayerId, belt); mutation.TryConsumeOne(PlayerId, belt); // 残り0にする

            // 全スロットを別アイテムで埋めて返却不能にする
            // Fill every slot with another item so the refund cannot be inserted
            var filler = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000005"));
            for (var i = 0; i < inventory.GetSlotSize(); i++) inventory.SetItem(i, ServerContext.ItemStackFactory.Create(filler, 1));
            mutation.Refill(PlayerId, belt, 2); // 残り2 → 次の撤去で凝縮

            Remove(packet, new Vector3Int(10, 0));

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0)));
            Assert.AreEqual(2, serviceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 設置数1のブロックは従来どおり全額返却される()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            SetItem(inventory, 0, Material1Guid, 2);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0)), new PacketResponseContext(null));

            Remove(packet, new Vector3Int(10, 0));

            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
        }

        private static void Remove(PacketResponseCreator packet, Vector3Int pos)
        {
            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(PlayerId, pos));
            packet.GetPacketResponse(payload, new PacketResponseContext(null));
        }
    }
}

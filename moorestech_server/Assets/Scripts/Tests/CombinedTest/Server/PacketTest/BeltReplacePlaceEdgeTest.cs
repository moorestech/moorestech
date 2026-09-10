using System;
using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 張替え設置の拒否系（空セル・ファミリー外・ロール不一致・未解放・コスト不足・満杯・通常設置不変）を検証する
    /// Verifies replace placement rejections (empty cell, non-family, role mismatch, locked, cost shortage, full inventory, normal placement unchanged)
    /// </summary>
    public class BeltReplacePlaceEdgeTest
    {
        [Test]
        public void 空セルへの張替えは何も設置しない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(58, 0, 58);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            // 張替えは既設の差し替えなので、既設が無いセルには新規設置しない
            // A replace swaps an existing block, so an empty cell never gets a fresh placement
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(pos));
        }

        [Test]
        public void ファミリー外の既設ブロックは張り替えられない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(60, 0, 60);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void ロールが一致しない手持ちは拒否される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(62, 0, 62);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyorSplitter);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 既設は直線、手持ちは分岐器（改造クライアント想定）。ロールが揃わないセルは触らない
            // Existing is a straight, held is a splitter (modded client); a cell whose roles differ is left alone
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void 未解放の手持ちは拒否される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(64, 0, 64);
            LockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void 新コスト不足のセルは失敗し既設が残る()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(66, 0, 66);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            // 撤去も財布操作も走らないので、既設は同じインスタンスのまま残る
            // Neither the removal nor the wallet runs, so the existing block survives as the very same instance
            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, block.BlockId);
            Assert.AreEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);
        }

        [Test]
        public void インベントリ満杯時は旧ブロックと搬送品が保持される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(68, 0, 68);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.BeltConveyorId);

            // 財布残0の有料ファミリーを撤去しても返却品は出ないが、搬送品の最悪ケース返却先が無いのでセルごと失敗する
            // Removing the paid family at wallet 0 refunds nothing, but the worst-case transit return has no room, so the whole cell fails
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            oldBelt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1), InsertItemContext.Empty);
            OccupyAllInventorySlots(serviceProvider, ForUnitTestItemId.ItemId1);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);
            Assert.AreEqual(1, BeltConveyorTransitCarryOver.Collect(oldBelt).Count);
        }

        [Test]
        public void IsReplace無しの通常設置は既設をスキップして挙動が変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(70, 0, 70);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var normalPlace = new List<PlaceInfo>
            {
                new()
                {
                    Position = pos,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = ForUnitTestModBlockId.GearBeltConveyor,
                    IsReplace = false,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(normalPlace), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.BeltConveyorId, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }
    }
}

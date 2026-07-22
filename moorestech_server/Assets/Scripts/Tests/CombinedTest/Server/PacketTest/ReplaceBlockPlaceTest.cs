using System;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;
using NUnit.Framework;
using UnityEngine;
using Core.Update;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// リプレース設置の正常系（向き変え・搬送品進行率維持・異種差額精算）を検証する
    /// Verifies the happy paths of replace placement (re-orient, transit progress carry-over, cross-type settlement)
    /// </summary>
    public class ReplaceBlockPlaceTest
    {
        // 同IDのままDirectionだけ変え、コスト返却と再消費が相殺され素材が増減しないこと
        // Change only the direction with the same id; refund and reconsumption cancel out so materials do not change
        [Test]
        public void 同型向き変えで素材増減なくDirectionだけ変わる()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(50, 0, 50);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.East), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, block.BlockId);
            Assert.AreEqual(BlockDirection.East, block.BlockPositionInfo.BlockDirection);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }

        // ベルト搬送品を進行させてからリプレースし、新ベルトに同アイテムが1スロット以内の進行率で残ること
        // Advance a belt item, replace, and confirm the new belt keeps the same item within one slot of progress
        [Test]
        public void 搬送品の進行率がリプレース後も維持される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(52, 0, 52);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.BeltConveyorId);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();

            // 入口に投入して数tick進行させる
            // Insert at the entry and let it progress a few ticks
            Assert.IsTrue(oldBelt.TryInsertItemWithRemainingRate(ForUnitTestItemId.ItemId2, ItemInstanceId.Create(), 1.0));
            GameUpdater.RunFrames(5);

            var oldItem = FindItem(oldBelt, ForUnitTestItemId.ItemId2);
            Assert.IsNotNull(oldItem);
            var oldRate = oldItem.RemainingTicks / (double)oldItem.TotalTicks;
            var oldInstanceId = oldItem.ItemInstanceId;

            // 向きを変えてリプレース（同IDでもDirectionが変わるので実リプレースが走る）
            // Replace with a different direction so an actual replace runs even for the same id
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.East), new PacketResponseContext(null));

            var newBelt = ServerContext.WorldBlockDatastore.GetBlock(pos).GetComponent<VanillaBeltConveyorComponent>();
            var newItem = FindItem(newBelt, ForUnitTestItemId.ItemId2);
            Assert.IsNotNull(newItem);
            var newRate = newItem.RemainingTicks / (double)newItem.TotalTicks;

            var slotTolerance = 1.0 / newBelt.BeltConveyorItems.Count;
            Assert.LessOrEqual(Math.Abs(newRate - oldRate), slotTolerance);
            Assert.AreEqual(oldInstanceId, newItem.ItemInstanceId);

            #region Internal

            IOnBeltConveyorItem FindItem(VanillaBeltConveyorComponent belt, ItemId itemId)
            {
                foreach (var item in belt.BeltConveyorItems)
                {
                    if (item != null && item.ItemId == itemId) return item;
                }
                return null;
            }

            #endregion
        }

        // 異種リプレースで旧コスト返却（+X）と新コスト消費（-Y）が精算されること
        // Cross-type replace settles the old-cost refund (+X) and the new-cost consumption (-Y)
        [Test]
        public void 異種置き換えで旧コスト返却と新コスト消費が精算される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(54, 0, 54);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            // コスト0のSmallGearBeltを置き、GearBelt分の素材を持たせる
            // Place the zero-cost SmallGearBelt and hold materials worth one GearBelt
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            // SmallGearBelt(X=0)→GearBelt(Y)：新コストYが消費される
            // SmallGearBelt(X=0) -> GearBelt(Y): the new cost Y is consumed
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.East), new PacketResponseContext(null));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            // GearBelt(Y)→SmallGearBelt(0)：旧コストYが返却される
            // GearBelt(Y) -> SmallGearBelt(0): the old cost Y is refunded
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }

        // 分岐器の設定とバッファ中の搬送品をリプレース後も保持する
        // Replacing a filter splitter preserves its settings and buffered transit item
        [Test]
        public void フィルター分岐器の設定と搬送品をリプレース後も保持する()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(56, 0, 56);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.FilterSplitter);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.FilterSplitter, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FilterSplitter, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldSplitter = oldBlock.GetComponent<VanillaFilterSplitterComponent>();
            for (var i = 0; i < oldSplitter.DirectionCount; i++) oldSplitter.SetMode(i, FilterSplitterMode.Default);
            var input = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1);
            oldSplitter.SetItem(0, input);
            oldSplitter.SetMode(0, FilterSplitterMode.Blacklist);

            var replaceService = new BlockReplaceService(serviceProvider.GetService<IGameUnlockStateDataController>());
            var replaceInfo = new PlaceInfoMessagePack(new PlaceInfo
            {
                Position = pos,
                Direction = BlockDirection.East,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = ForUnitTestModBlockId.FilterSplitter,
                IsReplace = true,
            });
            Assert.IsTrue(replaceService.TryReplaceBlock(replaceInfo, GetInventory(serviceProvider), true));

            var newBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);
            var newSplitter = newBlock.GetComponent<VanillaFilterSplitterComponent>();
            Assert.AreEqual(FilterSplitterMode.Blacklist, newSplitter.GetMode(0));
            var inventory = newBlock.GetComponent<IBlockInventory>();
            var hasTransitItem = false;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
            {
                var item = inventory.GetItem(i);
                if (item.Id != ForUnitTestItemId.ItemId2) continue;
                Assert.AreEqual(input.ItemInstanceId, item.ItemInstanceId);
                hasTransitItem = true;
                break;
            }
            Assert.IsTrue(hasTransitItem);
        }
    }
}

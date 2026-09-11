using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 張替え設置の正常系（ティア差し替え・向き維持・搬送品の引き継ぎ・コスト精算・分岐器ロール）を検証する
    /// Verifies replace placement happy paths (tier swap, direction kept, transit carry-over, cost settlement, splitter role)
    /// </summary>
    public class BeltReplacePlaceTest
    {
        [Test]
        public void 別ファミリーへ向きを保って差し替わり搬送品が引き継がれる()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(50, 0, 50);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            oldBelt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1), InsertItemContext.Empty);
            for (var i = 0; i < 20; i++) GameUpdater.UpdateOneTick();
            var before = BeltConveyorTransitCarryOver.Collect(oldBelt);
            Assert.AreEqual(1, before.Count);
            Assert.Less(before[0].RemainingRate, 1.0);
            Assert.Greater(before[0].RemainingRate, 0.0);

            // 手持ちの向きは無視され既設の向きが維持される
            // The held direction is ignored; the existing direction is kept
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, block.BlockId);
            Assert.AreEqual(BlockDirection.East, block.BlockPositionInfo.BlockDirection);
            Assert.AreNotEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);

            // 同一インスタンスの品が1個だけ新ベルトへ移る
            // Exactly one item with the same instance id moves onto the new belt
            var newBelt = block.GetComponent<IItemCollectableBeltConveyor>();
            var after = BeltConveyorTransitCarryOver.Collect(newBelt);
            Assert.AreEqual(1, after.Count);
            Assert.AreEqual(ForUnitTestItemId.ItemId2, after[0].ItemId);
            Assert.AreEqual(before[0].ItemInstanceId, after[0].ItemInstanceId);

            // 置いた直後の歯車ベルトはRPM供給前で搬送時間が無限大＝停止中なので、進行率は保存されず入口スロットへ入る
            // A freshly placed gear belt has an infinite transit time until RPM arrives, so the progress is not preserved and the item enters at the entry slot
            Assert.AreEqual(1.0, after[0].RemainingRate);
            var slots = newBelt.BeltConveyorItems;
            Assert.IsNotNull(slots[slots.Count - 1]);
            for (var i = 0; i < slots.Count - 1; i++) Assert.IsNull(slots[i]);

            // 搬送品はベルトへ戻っているのでプレイヤーへは1個も渡らない（増殖・ロストの検出）
            // The transit item went back onto the belt, so the player receives none of it (catches duplication and loss)
            Assert.AreEqual(0, CountItem(GetInventory(serviceProvider), ForUnitTestItemId.ItemId2));
        }

        [Test]
        public void 無動力の歯車ベルトへ張り替えても搬送品は下流へ搬出されない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(76, 0, 76);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);

            // 下流にチェストを繋いだ状態で張り替える。歯車ベルトはRPM未供給なので搬送は止まったままでなければならない
            // Replace with a chest wired downstream; the gear belt has no RPM yet, so transport must stay stopped
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, pos + new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            oldBlock.GetComponent<VanillaBeltConveyorComponent>().InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1), InsertItemContext.Empty);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            for (var i = 0; i < 30; i++) GameUpdater.UpdateOneTick();

            // 搬送品は新ベルトに留まり、チェストへもプレイヤーへも渡らない
            // The transit item stays on the new belt and reaches neither the chest nor the player
            var newBelt = ServerContext.WorldBlockDatastore.GetBlock(pos).GetComponent<IItemCollectableBeltConveyor>();
            Assert.AreEqual(1, BeltConveyorTransitCarryOver.Collect(newBelt).Count);
            Assert.AreEqual(0, CountItem(chest.GetComponent<VanillaChestComponent>(), ForUnitTestItemId.ItemId2));
            Assert.AreEqual(0, CountItem(GetInventory(serviceProvider), ForUnitTestItemId.ItemId2));
        }

        [Test]
        public void 無料ファミリーから有料ファミリーへの差し替えで新コストが消費される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(52, 0, 52);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
        }

        [Test]
        public void 分岐器は手持ちファミリーの分岐器へ差し替わる()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(54, 0, 54);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyorSplitter);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyorSplitter, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 既設・手持ちとも分岐器ロールなので、手持ちファミリーの分岐器へ差し替わる
            // Both the existing and the held block are splitters, so the swap lands on the held family's splitter
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, pos, BlockDirection.North), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, block.BlockId);
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(block.BlockId, out var family));
            Assert.IsTrue(family.TryGetRole(block.BlockId, out var role));
            Assert.AreEqual(BeltConveyorRole.Splitter, role);
        }

        [Test]
        public void 同ファミリー同ロールはno_opで素材もブロックも変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(56, 0, 56);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.East), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);
            Assert.AreEqual(BlockDirection.North, block.BlockPositionInfo.BlockDirection);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }

        [Test]
        public void ティアを下げる張替えで撤去返却がプレイヤーへ戻る()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(72, 0, 72);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            // 財布を通した設置で残りを2にしておく。次の撤去で1セット分が凝縮され返却が発生する
            // Place through the wallet so the remainder is 2; the next removal condenses one set's worth and produces a refund
            packet.GetPacketResponse(CreateNormalPlacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var oldBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            // 無料ティアへ下げたので、旧ブロックの返却素材がそのまま手元に残る
            // The tier dropped to a free family, so the old block's refunded materials stay in the player's hands
            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, block.BlockId);
            Assert.AreNotEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }

        [Test]
        public void 所持素材0でも撤去返却で新ティアのコストを賄える()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(74, 0, 74);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.LargeGearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            packet.GetPacketResponse(CreateNormalPlacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.LargeGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            // 所持は0でも撤去の返却が新コストへ充当されるので張替えは成立し、返却分はそのまま消費される
            // The holdings are zero, but the removal refund covers the new cost, so the replace succeeds and the refund is spent right back
            Assert.AreEqual(ForUnitTestModBlockId.LargeGearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.LargeGearBeltConveyor);
        }
    }
}

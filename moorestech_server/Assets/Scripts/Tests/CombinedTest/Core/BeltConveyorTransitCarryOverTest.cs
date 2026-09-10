using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using System;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// ベルト搬送品を進行率維持で別ベルトへ退避・復元できることを検証する
    /// Verifies belt transit items can be collected and restored into another belt with their progress preserved
    /// </summary>
    public class BeltConveyorTransitCarryOverTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 進行率を保ったまま別ベルトへ復元できる()
        {
            var source = CreateBelt(new Vector3Int(0, 0, 0));
            var inserted = source.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            Assert.AreEqual(0, inserted.Count);

            // 数tick進めて途中の進行率を作る
            // Advance a few ticks to create mid-way progress
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();

            var sourceItem = FindItem(source, ForUnitTestItemId.ItemId1);
            Assert.IsNotNull(sourceItem);
            var sourceRate = sourceItem.RemainingTicks / (double)sourceItem.TotalTicks;
            Assert.Less(sourceRate, 1.0);
            Assert.Greater(sourceRate, 0.0);

            var collected = BeltConveyorTransitCarryOver.Collect(source);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(sourceRate, collected[0].RemainingRate, 1e-9);

            var target = CreateBelt(new Vector3Int(0, 0, 5));
            var overflow = BeltConveyorTransitCarryOver.Restore(target, collected);

            Assert.AreEqual(0, overflow.Count);
            var restored = FindItem(target, ForUnitTestItemId.ItemId1);
            Assert.IsNotNull(restored);
            Assert.AreEqual(collected[0].ItemInstanceId, restored.ItemInstanceId);
            var restoredRate = restored.RemainingTicks / (double)restored.TotalTicks;
            Assert.AreEqual(collected[0].RemainingRate, restoredRate, 1.0 / restored.TotalTicks + 1e-9);
        }

        [Test]
        public void 中間の進行率は対応するスロットへ復元される()
        {
            // forUnitTestのTestBeltConveyorは総40tick・4スロット＝1スロット10tick
            // TestBeltConveyor in forUnitTest has 40 total ticks over 4 slots, i.e. 10 ticks per slot
            var (slotCount, totalTicks) = GetBeltShape();
            Assert.AreEqual(4, slotCount);
            Assert.AreEqual(40u, totalTicks);

            // 残り15tickは滞在条件「10 < 15 <= 20」によりindex1に属する。両端(index0/3)では逆算式の誤りを検出できない
            // 15 remaining ticks belongs to index 1 by the dwell rule 10 < 15 <= 20; the ends (index 0/3) cannot detect an off-by-one
            var target = CreateBelt(new Vector3Int(0, 0, 20));
            var overflow = BeltConveyorTransitCarryOver.Restore(target, new List<BeltTransitItem>
            {
                new(ForUnitTestItemId.ItemId1, ItemInstanceId.Create(), 15 / 40.0),
            });

            Assert.AreEqual(0, overflow.Count);
            Assert.IsNull(target.BeltConveyorItems[0]);
            Assert.IsNotNull(target.BeltConveyorItems[1]);
            Assert.IsNull(target.BeltConveyorItems[2]);
            Assert.IsNull(target.BeltConveyorItems[3]);
            Assert.AreEqual(15u, target.BeltConveyorItems[1].RemainingTicks);
            Assert.AreEqual(40u, target.BeltConveyorItems[1].TotalTicks);
        }

        [Test]
        public void 入り切らなかった分はoverflowとして返る()
        {
            var (slotCount, totalTicks) = GetBeltShape();
            var target = CreateBelt(new Vector3Int(0, 0, 10));

            // 進行率の異なる4件は各スロットへ収まり、同じ入口を狙う残り2件は入口が塞がって溢れる
            // Four items with distinct progress fill every slot, and the remaining two aiming at the same entry overflow
            var items = new List<BeltTransitItem>();
            for (var i = 0; i < slotCount; i++)
            {
                items.Add(new BeltTransitItem(ForUnitTestItemId.ItemId1, ItemInstanceId.Create(), (slotCount - i) / (double)slotCount));
            }
            var overflowSources = new List<BeltTransitItem>
            {
                new(ForUnitTestItemId.ItemId2, ItemInstanceId.Create(), 1.0),
                new(ForUnitTestItemId.ItemId3, ItemInstanceId.Create(), 1.0),
            };
            items.AddRange(overflowSources);

            var overflow = BeltConveyorTransitCarryOver.Restore(target, items);

            Assert.AreEqual(2, overflow.Count);
            Assert.AreEqual(overflowSources[0].ItemInstanceId, overflow[0].ItemInstanceId);
            Assert.AreEqual(overflowSources[1].ItemInstanceId, overflow[1].ItemInstanceId);
            Assert.AreEqual(slotCount, CountItems(target));

            // 出口側(index0)ほど残りtickが少ない単調性が保たれている
            // The exit side (index 0) keeps fewer remaining ticks, preserving monotonicity
            var ticksPerSlot = totalTicks / (uint)slotCount;
            for (var i = 0; i < slotCount; i++)
            {
                Assert.AreEqual((uint)(i + 1) * ticksPerSlot, target.BeltConveyorItems[i].RemainingTicks);
            }
        }

        private static (int slotCount, uint totalTicks) GetBeltShape()
        {
            var param = (BeltConveyorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam;
            return (param.BeltConveyorItemCount, GameUpdater.SecondsToTicks(param.TimeOfItemEnterToExit));
        }

        private static VanillaBeltConveyorComponent CreateBelt(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block.GetComponent<VanillaBeltConveyorComponent>();
        }

        private static IOnBeltConveyorItem FindItem(VanillaBeltConveyorComponent belt, ItemId itemId)
        {
            foreach (var item in belt.BeltConveyorItems)
            {
                if (item != null && item.ItemId == itemId) return item;
            }
            return null;
        }

        private static int CountItems(VanillaBeltConveyorComponent belt)
        {
            var count = 0;
            foreach (var item in belt.BeltConveyorItems)
            {
                if (item != null) count++;
            }
            return count;
        }
    }
}

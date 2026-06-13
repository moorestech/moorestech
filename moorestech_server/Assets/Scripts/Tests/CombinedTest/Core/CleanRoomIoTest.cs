using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomIoTest
    {
        // ハッチが入力面から受けたアイテムを出力面のターゲットへ中継し、レートを公開する
        // Hatch relays an item from the input side to the output-side target and reports throughput
        [Test]
        public void ItemHatch_RelaysItemAndReportsThroughput()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ハッチを (0,0,0)、出力面(+X)側ターゲット(チェスト)を (1,0,0) に置く
            // Place hatch at (0,0,0) and the output-side chest target at (1,0,0)
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetChest);

            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            // 入力面ソースの代役として、ハッチに直接 InsertItem する
            // Insert directly into the hatch as a stand-in for the input-side source
            var item = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var remain = hatch.InsertItem(item, InsertItemContext.Empty);
            Assert.AreEqual(0, remain.Count, "Hatch accepts the item into its in-transit buffer");

            // 中継が完了するまで tick を回す
            // Tick until the relay completes
            GameUpdater.RunFrames(5);

            // ターゲットチェストにアイテムが届いている
            // The item has arrived in the target chest
            Assert.True(targetChest.TryGetComponent<IBlockInventory>(out var chestInv));
            var arrived = Enumerable.Range(0, chestInv.GetSlotSize())
                .Sum(i => chestInv.GetItem(i).Count);
            Assert.AreEqual(1, arrived, "Relayed item reaches the target inventory");

            // レート窓に搬送が反映されている（直近窓に1個 → >0）
            // Throughput window reflects the relay (1 item in the recent window → >0)
            Assert.Greater(hatch.RecentThroughputPerSecond, 0.0);
        }

        // 満杯のハッチは受け取りを拒否し差し戻す（低スループットの根拠）
        // A full hatch rejects insertion and hands the stack back (the basis of low throughput)
        [Test]
        public void ItemHatch_RejectsWhenInTransitBufferIsFull()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ターゲット無しで設置 → 中継が完了せずバッファに溜まる
            // Place without a target so items stay in-transit
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            for (var i = 0; i < CleanRoomItemHatchComponent.MaxInTransitStacks; i++)
            {
                var r = hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
                Assert.AreEqual(0, r.Count);
            }

            // 上限到達後は InsertionCheck=false・InsertItem は差し戻し
            // Once full, InsertionCheck is false and InsertItem hands the stack back untouched
            Assert.False(hatch.InsertionCheck(new List<IItemStack>()));
            var rejected = hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            Assert.AreEqual(1, rejected.Count, "Full buffer rejects further insertion");
        }

        // 搬送停止後、レート窓1周（20tick）で RecentThroughputPerSecond は 0 へ戻る
        // After relays stop, throughput decays to zero within one full window (20 ticks)
        [Test]
        public void ItemHatch_ThroughputDecaysToZeroAfterIdleWindow()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            GameUpdater.RunFrames(2);
            Assert.Greater(hatch.RecentThroughputPerSecond, 0.0, "Relay lands in the window");

            // 窓1周ぶん無搬送で回す → 0 に減衰
            // Run one full idle window → decays to zero
            GameUpdater.RunFrames(CleanRoomItemHatchComponent.HatchRateWindowTicks + 1);
            Assert.AreEqual(0.0, hatch.RecentThroughputPerSecond, 1e-9);
        }
    }
}

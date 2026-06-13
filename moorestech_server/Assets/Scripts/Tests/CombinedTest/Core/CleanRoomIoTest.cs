using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Fluid;
using Game.Fluid;
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

        // パイプハッチが inflow 面から受けた流体を outflow 面のパイプへ中継する
        // Pipe hatch relays fluid received on the inflow face to the outflow-side pipe
        [Test]
        public void PipeHatch_RelaysFluidToOutflowSide()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ハッチを (0,0,0)、outflow(+X) 側パイプを (1,0,0) に置く
            // Hatch at (0,0,0), outflow-side pipe at (1,0,0)
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var outflowPipe);

            Assert.True(hatchBlock.TryGetComponent<CleanRoomPipeHatchComponent>(out var hatch));

            // 外パイプの代役として、ハッチへ直接 AddLiquid する（流体IDは既存 FluidTest と同じGUID流儀）
            // Add fluid directly to the hatch; resolve the fluid id the same way as the existing FluidTest
            var fluidId = MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000001"));
            var stack = new FluidStack(50.0, fluidId);
            hatch.AddLiquid(stack, FluidContainer.Empty);

            // 中継が進むまで tick を回す（テストmodパイプの flowCapacity=10 → 0.5/tick）
            // Tick until the relay propagates (test-mod pipe flowCapacity=10 → 0.5/tick)
            GameUpdater.RunFrames(10);

            // outflow 側パイプに流体が届いている
            // Fluid has arrived in the outflow-side pipe
            Assert.True(outflowPipe.TryGetComponent<IFluidInventory>(out var outflowInv));
            var outflowAmount = outflowInv.GetFluidInventory().Sum(f => f.Amount);
            Assert.Greater(outflowAmount, 0.0, "Relayed fluid reaches the outflow-side pipe");
        }

        // ドアハッチは通過を合算して次tickで latch し、peek は非破壊、さらに次の latch で 0 に戻る
        // The door hatch latches accumulated passages on the next tick; peek is non-destructive; the next latch clears it
        [Test]
        public void DoorHatch_PassageBurstLatchesForExactlyOneTick()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomDoorHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var doorBlock);

            // ドアハッチは密閉境界マーカーを持つ（0.3 のドア整合）
            // The door hatch carries the sealing-boundary marker (door reconciliation §0.3)
            Assert.True(doorBlock.TryGetComponent<ICleanRoomBoundaryComponent>(out var marker));
            Assert.AreEqual(CleanRoomBoundaryKind.DoorHatch, marker.BoundaryKind);

            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorHatchComponent>(out var door));

            // 2回通過 → latch 前は 0、latch 後は 2 * burst_door(15) = 30
            // Two passages → 0 before the latch, 30 (= 2 * 15) after
            door.NotifyPlayerPassage();
            door.NotifyPlayerPassage();
            Assert.AreEqual(0.0, door.PeekPendingBurst(), 1e-9, "Not visible until latched");

            GameUpdater.RunFrames(1);
            Assert.AreEqual(30.0, door.PeekPendingBurst(), 1e-9, "Latched for this tick");
            Assert.AreEqual(30.0, door.PeekPendingBurst(), 1e-9, "Peek is non-destructive");

            // 次の latch で 0（公開はちょうど1tick分）
            // Cleared by the next latch (visible for exactly one tick)
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0.0, door.PeekPendingBurst(), 1e-9);
        }
    }
}

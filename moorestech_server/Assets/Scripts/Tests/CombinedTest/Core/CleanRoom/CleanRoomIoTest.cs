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
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.CleanRoom;
using Game.CleanRoom.Machine;
using Game.Context;
using Game.EnergySystem;
using Game.SaveLoad.Interface;
using Game.World.Interface.DataStore;
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

        // 境界ハッチから「面する部屋」を引ける。境界セル自体は部屋に属さない
        // A boundary hatch resolves its facing room(s); the boundary cell itself belongs to no room
        [Test]
        public void AdjacentRooms_BoundaryHatchResolvesItsFacingRoom()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 壁シェル (0,0,0)-(4,4,4)。x=0 面の壁1枚 (0,2,2) をアイテムハッチに置換（出力+Xが室内向き＝搬入用）
            // Wall shell (0,0,0)-(4,4,4); replace one x=0 wall block with an item hatch (output +X faces inside = import)
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatchId);
            GameUpdater.RunFrames(2);

            Assert.True(world.TryGetBlock(new Vector3Int(0, 2, 2), out IBlock hatchBlock));

            // 境界セルは部屋に属さない（部屋内クエリは false）
            // The boundary cell belongs to no room (the in-room query returns false)
            Assert.False(datastore.TryGetCleanRoomAt(new Vector3Int(0, 2, 2), out _));

            // 面する部屋はちょうど1つで、室内セルの部屋と一致する
            // Exactly one facing room, identical to the interior cell's room
            var adjacent = datastore.GetAdjacentCleanRooms(hatchBlock);
            Assert.AreEqual(1, adjacent.Count);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room));
            Assert.AreSame(room, adjacent[0]);
        }

        // 搬入ハッチの搬送が続く窓では、無搬送窓より N の増分が大きい（A_hatch がレートとして効く）
        // While the import hatch keeps relaying, N grows faster than in the idle window (A_hatch as a rate term)
        [Test]
        public void Pollution_ImportHatchThroughputRaisesImpurity()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // x=0 面ハッチ＋室内チェスト (1,2,2)（ハッチ出力+X の先）
            // Hatch on the x=0 face + interior chest at (1,2,2) (ahead of the hatch's +X output)
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatchId);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryGetBlock(new Vector3Int(0, 2, 2), out IBlock hatchBlock);
            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));
            GameUpdater.RunFrames(2);

            // 無搬送の10tick窓の N 増分（恒常項のみ）
            // N delta over a 10-tick idle window (continuous terms only)
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room0));
            var n0 = room0.ImpurityCount;
            GameUpdater.RunFrames(10);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room1));
            var idleDelta = room1.ImpurityCount - n0;

            // 毎tick1個搬入してレート窓（20tick）を飽和させてから測定窓に入る（窓ランプの過小評価を排除）
            // Feed one item per tick to saturate the 20-tick rate window before measuring (avoids window-ramp underestimation)
            for (var i = 0; i < CleanRoomItemHatchComponent.HatchRateWindowTicks; i++)
            {
                hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
                GameUpdater.RunFrames(1);
            }
            Assert.Greater(hatch.RecentThroughputPerSecond, 0.0, "throughput saturated before the measurement window");

            // 飽和後の10tick窓の N 増分（毎tick搬入継続）
            // N delta over a 10-tick window after saturation (keep feeding each tick)
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room2));
            var n1 = room2.ImpurityCount;
            for (var i = 0; i < 10; i++)
            {
                hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
                GameUpdater.RunFrames(1);
            }
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room3));
            var activeDelta = room3.ImpurityCount - n1;

            // A_hatch ≈ k_hatch(0.30) × スループット の分だけ増分が上回る
            // The delta exceeds idle by ~k_hatch × throughput
            Assert.Greater(activeDelta, idleDelta + 1.0, "A_hatch raises N while throughput is positive");
        }

        // 搬出ハッチ（max-X 面・入力が室内向き）でもスループットが N に計上される（0.8 の向き仕様）
        // An export hatch (max-X face, input facing inside) also books its throughput into N (§0.8)
        [Test]
        public void Pollution_ExportHatchThroughputAlsoRaisesImpurity()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // x=4 面の壁1枚 (4,2,2) をハッチに置換（入力−X が室内向き＝搬出用）。室内チェストが自動 push する
            // Replace one x=4 wall block with a hatch (input −X faces inside = export); the interior chest auto-pushes
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(4, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatchId);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(3, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var innerChest);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(5, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(2);

            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room0));
            var n0 = room0.ImpurityCount;
            GameUpdater.RunFrames(10);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room1));
            var idleDelta = room1.ImpurityCount - n0;

            // 室内チェストへアイテムを補充 → チェストがハッチへ自動 push → ハッチが外チェストへ中継
            // Stock the interior chest → it auto-pushes into the hatch → the hatch relays outward
            Assert.True(innerChest.TryGetComponent<IBlockInventory>(out var chestInv));
            var n1 = room1.ImpurityCount;
            chestInv.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 10));
            GameUpdater.RunFrames(10);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room2));
            var activeDelta = room2.ImpurityCount - n1;

            Assert.Greater(activeDelta, idleDelta + 0.5, "Export throughput also counts toward A_hatch");
        }

        // 通過1回でちょうど burst_door(15) が N へ加算され、以降のtickで再加算されない
        // One passage adds exactly burst_door (15) to N, with no re-addition on later ticks
        [Test]
        public void Pollution_DoorPassageAddsBurstToImpurityExactlyOnce()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomDoorHatchId);
            world.TryGetBlock(new Vector3Int(0, 2, 2), out IBlock doorBlock);
            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorHatchComponent>(out var door));
            GameUpdater.RunFrames(2);

            // 無通過の2tick窓の増分（恒常項のみ。除去0なので毎tick一定）
            // Delta over an idle 2-tick window (continuous terms only; constant per tick with zero removal)
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room0));
            var n0 = room0.ImpurityCount;
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room1));
            var idleDelta = room1.ImpurityCount - n0;

            // 通過1回 → 2tick以内に latch→計上が完了し、増分 = idleDelta + 15
            // One passage → latch + booking complete within 2 ticks; delta = idleDelta + 15
            var n1 = room1.ImpurityCount;
            door.NotifyPlayerPassage();
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room2));
            var burstDelta = room2.ImpurityCount - n1;
            Assert.AreEqual(15.0, burstDelta - idleDelta, 1e-6, "Exactly burst_door lands in N");

            // さらに2tick → 増分は恒常項のみ（バーストの二重計上なし）
            // Two more ticks → only the continuous terms (no double-booking of the burst)
            var n2 = room2.ImpurityCount;
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room3));
            Assert.AreEqual(idleDelta, room3.ImpurityCount - n2, 1e-6, "No re-addition on later ticks");
        }

        // 2部屋の共有境界にあるドアハッチは、面する両部屋へ全額バーストを加算する（0.5 の確定規則）
        // A door hatch on a shared boundary books the full burst into every facing room (§0.5)
        [Test]
        public void Pollution_SharedBoundaryDoorBurstsAllFacingRooms()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // x=4 平面を共有する2つの壁シェル。共有壁の1枚 (4,2,2) をドアハッチに置換
            // Two shells sharing the x=4 plane; replace one shared-wall block (4,2,2) with a door hatch
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            BuildWallShell(world, new Vector3Int(4, 0, 0), new Vector3Int(8, 4, 4));
            ReplaceWith(world, new Vector3Int(4, 2, 2), ForUnitTestModBlockId.CleanRoomDoorHatchId);
            world.TryGetBlock(new Vector3Int(4, 2, 2), out IBlock doorBlock);
            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorHatchComponent>(out var door));
            GameUpdater.RunFrames(2);

            // 面する部屋は2つ
            // The door faces two rooms
            Assert.AreEqual(2, datastore.GetAdjacentCleanRooms(doorBlock).Count);

            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var roomA0));
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(6, 2, 2), out var roomB0));
            var a0 = roomA0.ImpurityCount;
            var b0 = roomB0.ImpurityCount;
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var roomA1));
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(6, 2, 2), out var roomB1));
            var idleDeltaA = roomA1.ImpurityCount - a0;
            var idleDeltaB = roomB1.ImpurityCount - b0;

            // 通過1回 → 両部屋とも +15（全額。按分しない）
            // One passage → both rooms gain the full +15 (no splitting)
            var a1 = roomA1.ImpurityCount;
            var b1 = roomB1.ImpurityCount;
            door.NotifyPlayerPassage();
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var roomA2));
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(6, 2, 2), out var roomB2));
            Assert.AreEqual(15.0, (roomA2.ImpurityCount - a1) - idleDeltaA, 1e-6, "Room A books the full burst");
            Assert.AreEqual(15.0, (roomB2.ImpurityCount - b1) - idleDeltaB, 1e-6, "Room B books the full burst");
        }

        // I-1: 密閉室内で稼働中の専用機械は A_machine(2.0/s) 分だけ N 増分を増やす（同一ジオメトリの稼働/非稼働窓を比較）
        // I-1: a running dedicated machine raises the N delta by ~A_machine (2.0/s) vs an idle window on the SAME geometry
        [Test]
        public void Pollution_RunningMachineRaisesImpurityByAMachine()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 密閉室 (0,0,0)-(6,4,6)。機械を内部 (2,2,2) に設置（占有1セル）。フィルター無し＝除去0で N は単調増加
            // Sealed room; place the machine at an interior cell. No filter -> zero removal so N grows monotonically
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(6, 4, 6));
            var recipe = FindExposureRecipe();
            var machineBlockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            world.TryAddBlock(machineBlockId, new Vector3Int(2, 2, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            GameUpdater.RunFrames(2);

            var proc = machineBlock.GetComponent<CleanRoomMachineProcessorComponent>();
            var electric = machineBlock.GetComponent<CleanRoomMachineElectricComponent>();
            var inventory = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();

            Assert.True(datastore.TryGetCleanRoom(machineBlock, out var room), "machine belongs to one room");

            // 機械が止まっている（入力なし＝Idle）窓の N 増分 — 同一ジオメトリの基準
            // Idle window N delta (no inputs -> Idle) on the SAME geometry as the running window
            const int window = 20;
            var nIdle0 = room.ImpurityCount;
            for (var i = 0; i < window; i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000f));
                GameUpdater.UpdateOneTick();
            }
            var idleDelta = room.ImpurityCount - nIdle0;
            Assert.AreEqual(ProcessState.Idle, proc.CurrentState, "machine idle without inputs");

            // 入力＋給電を与えて稼働させ、同じ長さの窓で N 増分を測る
            // Load inputs + power so the machine runs; measure the N delta over an identical window
            foreach (var inputItem in recipe.InputItems)
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count * 50));
            // 稼働開始させてから測定窓に入る（Processing 中の汚染を測る）
            // Get it into Processing before the measurement window (measure pollution while Processing)
            for (var i = 0; i < 5; i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000f));
                GameUpdater.UpdateOneTick();
            }
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState, "machine running with inputs+power");

            var nRun0 = room.ImpurityCount;
            for (var i = 0; i < window; i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000f));
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(ProcessState.Processing, proc.CurrentState, "stays running across the window");
            }
            var runDelta = room.ImpurityCount - nRun0;

            // 稼働窓は A_machine·dt·window = 2.0 × (1/20) × 20 = 2.0 ぶん増分が大きい（除去0なので一定）
            // The running window adds A_machine·dt·window = 2.0 × (1/20) × 20 = 2.0 more than idle (constant, zero removal)
            var expectedExtra = 2.0 * GameUpdater.SecondsPerTick * window;
            Assert.AreEqual(expectedExtra, runDelta - idleDelta, 1e-6, "Running machine adds A_machine to the room's N rate");
        }

        // ハッチが中継待ちアイテムを保持した状態でsave→loadし、アイテムが復元される
        // Save while the hatch holds in-transit items, then load and confirm they are restored
        [Test]
        public void ItemHatch_InTransitItemsSurviveSaveLoad()
        {
            // --- セーブ側ワールド ---
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;

            // ターゲットを置かず、ハッチに中継待ちを溜めたまま保存する（中継が完了しないように）
            // Save with no target so items stay in-transit (relay cannot complete)
            worldA.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchA);
            Assert.True(hatchA.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchCompA));
            hatchCompA.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3), InsertItemContext.Empty);
            GameUpdater.RunFrames(2); // ターゲット無し → バッファに残る

            var json = providerA.GetService<global::Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            // --- ロード側ワールド ---
            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (providerB.GetService<IWorldSaveDataLoader>() as global::Game.SaveLoad.Json.WorldLoaderFromJson).Load(json); // 具象型はDI未登録（IWorldSaveDataLoaderで解決。フェーズ2 C-2と同じ罠）

            var worldB = ServerContext.WorldBlockDatastore;
            Assert.True(worldB.TryGetBlock(new Vector3Int(0, 0, 0), out IBlock hatchB));
            Assert.True(hatchB.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchCompB));

            // 復元後、中継待ちの3個が残っている
            // After load, the 3 in-transit items are restored
            var restored = 0;
            for (var i = 0; i < hatchCompB.GetSlotSize(); i++) restored += hatchCompB.GetItem(i).Count;
            Assert.AreEqual(3, restored);
        }

        // パイプハッチが内部流体を保持した状態でsave→loadし、量が復元される
        // Save while the pipe hatch holds fluid, then load and confirm the amount is restored
        [Test]
        public void PipeHatch_BufferedFluidSurvivesSaveLoad()
        {
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;
            worldA.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipeA);
            Assert.True(pipeA.TryGetComponent<CleanRoomPipeHatchComponent>(out var compA));

            var fluidId = global::Core.Master.MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000001"));
            compA.AddLiquid(new global::Game.Fluid.FluidStack(40.0, fluidId), global::Game.Fluid.FluidContainer.Empty);
            // 接続先なし・tickも進めない → 内部コンテナに 40 が残ったまま
            // No target, no ticks → 40 stays in the inner container

            var json = providerA.GetService<global::Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (providerB.GetService<IWorldSaveDataLoader>() as global::Game.SaveLoad.Json.WorldLoaderFromJson).Load(json); // 具象型はDI未登録（IWorldSaveDataLoaderで解決。フェーズ2 C-2と同じ罠）

            Assert.True(ServerContext.WorldBlockDatastore.TryGetBlock(new Vector3Int(0, 0, 0), out IBlock pipeB));
            Assert.True(pipeB.TryGetComponent<CleanRoomPipeHatchComponent>(out var compB));
            Assert.AreEqual(40.0, compB.GetFluidInventory().Sum(f => f.Amount), 1e-6);
        }

        // I/Oブロックが境界に在っても純度セーブ(CleanRoomSaveData)はそのまま round-trip する（スキーマ改変不要）
        // Purity save (CleanRoomSaveData) round-trips even with I/O blocks on the boundary (no schema change)
        [Test]
        public void CleanRoomSave_RoundTripsWithIoBlocksPresent()
        {
            // --- セーブ側: 壁シェルの1面をハッチに置換した密閉部屋を作り、N をシードする ---
            // Save side: a sealed room with one wall face replaced by a hatch; seed N
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;
            var datastoreA = providerA.GetService<global::Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(worldA, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(worldA, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatchId);
            GameUpdater.RunFrames(2);

            var insideCell = new Vector3Int(2, 2, 2);
            Assert.True(datastoreA.TryGetCleanRoomAt(insideCell, out var roomA));

            // 既知の N をシードし、1tick回して閾値行/状態を最新化してから期待値を採取する
            // Seed a known N, run one tick so threshold/status settle, then capture expectations
            roomA.AddImpurity(123.0);
            GameUpdater.RunFrames(1);
            Assert.True(datastoreA.TryGetCleanRoomAt(insideCell, out var roomA2));
            var expectedN = roomA2.ImpurityCount;
            var expectedThresholdIndex = roomA2.ThresholdIndex;
            var expectedStatus = roomA2.Status;

            var json = providerA.GetService<global::Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            // --- ロード側: RunFrames を挟まずロード直後に厳密一致を検証する ---
            // Load side: assert exact equality right after Load, before any RunFrames
            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (providerB.GetService<IWorldSaveDataLoader>() as global::Game.SaveLoad.Json.WorldLoaderFromJson).Load(json); // 具象型はDI未登録（IWorldSaveDataLoaderで解決。フェーズ2 C-2と同じ罠）
            var datastoreB = providerB.GetService<global::Game.CleanRoom.CleanRoomDatastore>();

            Assert.True(datastoreB.TryGetCleanRoomAt(insideCell, out var roomB));
            Assert.AreEqual(expectedN, roomB.ImpurityCount, 1e-6, "N restored exactly (no ticks yet)");
            Assert.AreEqual(expectedThresholdIndex, roomB.ThresholdIndex, "Threshold row restored");
            Assert.AreEqual(expectedStatus, roomB.Status, "Status restored");

            // --- 1tick 進めてもリセットされない（dirty 再々検出事故の非回帰。balance §6）---
            // One tick later, nothing resets (non-regression for the dirty re-detection accident; balance §6)
            GameUpdater.RunFrames(1);
            Assert.True(datastoreB.TryGetCleanRoomAt(insideCell, out var roomB2));
            // 恒常汚染で僅かに増えるのは正常。リセット(→~0)や二重復元(→~2倍)を弾く許容窓で判定する
            // A slight increase from continuous pollution is normal; the window rejects resets (~0) and double-restores (~2x)
            Assert.GreaterOrEqual(roomB2.ImpurityCount, expectedN - 1e-6, "N not reset by the first tick");
            Assert.Less(roomB2.ImpurityCount, expectedN + 2.0, "No reset/double-restore sized jump");
            Assert.AreEqual(expectedThresholdIndex, roomB2.ThresholdIndex, "Threshold row survives the first tick");
        }


        // 露光レシピ（CleanRoomMachine）のレシピ要素を見つける。
        // Find the exposure-recipe element (the CleanRoom machine).
        private static Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement FindExposureRecipe()
        {
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            foreach (var o in r.OutputItems)
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(r.MachineRecipeGuid, o.ItemGuid, out _)) return r;
            throw new Exception("exposure recipe not found");
        }

        // min..max の外殻だけ壁を置き、内部を空洞にするヘルパ（フェーズ1テストからコピー）。
        // Helper: place walls only on the shell of [min,max], leaving the interior hollow (copied from the phase-1 test).
        private static void BuildWallShell(IWorldBlockDatastore world, Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var onShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!onShell) continue;
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(x, y, z),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }
        }

        // 既存ブロックを撤去して別ブロックを同セルに設置する（壁→ハッチ置換）。
        // Remove the existing block and place another at the same cell (wall -> hatch swap).
        private static void ReplaceWith(IWorldBlockDatastore world, Vector3Int pos, BlockId blockId)
        {
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            world.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

    }
}

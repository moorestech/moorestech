using System;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.CleanRoom.Pollution;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPollutionTest
    {
        // 基準部屋座標（内寸 5x5x3 を [0..6]x[0..4]x[0..6] の外殻で囲う）。
        // Reference-room coordinates: 5x5x3 cavity inside a [0..6]x[0..4]x[0..6] shell.
        private static readonly Vector3Int ShellMin = new(0, 0, 0);
        private static readonly Vector3Int ShellMax = new(6, 4, 6);

        // 接続点2（ItemHatch + PipeHatch を境界壁2枚と差し替え。どちらも気密境界なので密閉維持）。
        // Connectors=2: swap two boundary walls for ItemHatch + PipeHatch (both airtight, seal preserved).
        private static readonly Vector3Int ItemHatchPos = new(0, 2, 2);   // 内部(1,2,2)に面する / faces interior (1,2,2)
        private static readonly Vector3Int PipeHatchPos = new(6, 2, 2);   // 内部(5,2,2)に面する / faces interior (5,2,2)

        // エアフィルター: 側壁に接しない床セル（x,z∈[2,4], y=1）。V=74, S=109 になる。
        // Air filter on a floor cell not touching side walls (x,z in [2,4], y=1) => V=74, S=109.
        private static readonly Vector3Int AirFilterPos = new(3, 1, 3);

        // 部屋帰属確認用の内部空セル（フィルターと別セル）。
        // An interior empty cell (distinct from the filter) for room lookup.
        private static readonly Vector3Int InsideEmptyCellPos = new(1, 1, 1);

        // 電柱は床下に置きフィルターへ届かせる（壁越しでも幾何距離のみで接続）。発電機は電柱隣。
        // Pole below the floor reaches the filter (power connects geometrically through walls); generator beside the pole.
        private static readonly Vector3Int PolePos = new(3, -3, 3);       // 1x3x1 → y=-3,-2,-1 を占有 / occupies y=-3..-1
        private static readonly Vector3Int GeneratorPos = new(4, -1, 3);

        // n=2 用フィルター座標（どちらも側壁に接しない床セル）。
        // Two filter cells for the n=2 test (both interior floor, not touching side walls).
        private static readonly Vector3Int AirFilterPos1 = new(2, 1, 2);
        private static readonly Vector3Int AirFilterPos2 = new(4, 1, 4);

        [Test]
        public void ComputeATotal_ReferenceRoom_MachineLess()
        {
            // 基準部屋(機械なし): V=74, S=109, 接続点2(ItemHatch1+PipeHatch1), ハッチ搬送0。
            // Machine-less reference room: V=74, S=109, connectors=2, hatch throughput 0.
            var aTotal = CleanRoomPollutionCalculator.ComputeATotal(
                volume: 74, surfaceArea: 109, connectorCount: 2, runningMachineCount: 0,
                hatchThroughputPerSecond: 0.0);

            // 0.10*74 + 0.05*109 + 0.50*2 = 13.85
            Assert.AreEqual(13.85, aTotal, 1e-9);
        }

        [Test]
        public void ComputeATotal_MachineTermAddsTwoPerRunningMachine()
        {
            // A_machine=2.0 個/(稼働機械·秒) の係数を固定（実機械の配線はフェーズ4）。
            // Pin the A_machine=2.0 coefficient; actual machine wiring lands in phase 4.
            var withMachine = CleanRoomPollutionCalculator.ComputeATotal(74, 109, 2, 1, 0.0);
            Assert.AreEqual(15.85, withMachine, 1e-9);
        }

        [Test]
        public void AirFilter_PoweredInSealedReferenceRoom_EquilibratesAndWearsFilter()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 内寸 5x5x3 を壁で囲い、ItemHatch 1 + PipeHatch 1 を境界に差し込む（接続点2）。
            // Seal a 5x5x3 cavity; swap 2 wall blocks for ItemHatch + PipeHatch (connectors=2).
            BuildReferenceRoom(world);

            // 先に電柱+無限発電機を設置（機械設置時に既存ポールを探す経路で確実に接続する）。
            // Place pole + generator first so the machine-place path finds the existing pole and connects reliably.
            PlacePoleAndInfinityGenerator(world);

            // エアフィルター1台を側壁に接しない床セルへ（V=74, S=109）。
            // One air filter on a floor cell not touching side walls (V=74, S=109).
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, AirFilterPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var filterBlock);
            var filterComponent = filterBlock.GetComponent<CleanRoomAirFilterComponent>();
            var filterInventory = filterBlock.GetComponent<CleanRoomAirFilterItemComponent>();

            // フィルター2個投入（摩耗は filterCapacity=5000 未満に収まり消費は起きない想定）。
            // Load 2 filters; expected wear stays below filterCapacity=5000 (no consumption).
            filterInventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));

            // τ=V/(nq)=74/5≈14.8s。±10%摩耗帯は t≥10τ で成立するため 300s（6000tick≈20τ）回す。
            // τ≈14.8s; run 300s (6000 ticks ≈ 20τ) so the ±10% wear band holds (needs t ≥ 10τ).
            GameUpdater.RunFrames(6000);

            Assert.IsTrue(datastore.TryGetCleanRoomAt(InsideEmptyCellPos, out var room), "room exists");
            Assert.AreEqual(74, room.Volume, "V=74 (占有セル除外)");
            Assert.AreEqual(109, room.SurfaceArea, "S=109");

            // 電力が届いていることのサニティ（給電不足だと nq<5 で C が跳ね上がる）。
            // Sanity: power reaches the filter (under-power would push nq<5 and C far higher).
            Assert.Greater(filterComponent.RemovalVolumePerSecond, 0.0, "filter is powered");

            // C_eq = 13.85/5 = 2.77（±0.3）。閾値行はA(index 0)。ACH=5/74≈0.0676≥0.0167。
            // C_eq=2.77 (±0.3); threshold row A (index 0); ACH satisfied.
            Assert.AreEqual(2.77, room.Concentration, 0.3, "equilibrium concentration ~2.77");
            Assert.AreEqual(0, room.ThresholdIndex, "threshold row A");

            // 摩耗配線の検証（必須）: A_total·t=4155 の±10%帯。理論値 ≈ 4155−N_eq ≈ 3950。
            // Wear-wiring assertion (mandatory): within ±10% of A_total·t=4155; theory ≈ 3950.
            Assert.That(filterComponent.WearProgress, Is.InRange(3739.5, 4570.5), "wear ≈ A_total×t (±10%)");
            Assert.AreEqual(2, filterInventory.FilterCount, "5000未満なのでフィルター未消費");
        }

        [Test]
        public void AirFilter_TwoUnits_RemovalAddsAndWearIsShared()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildReferenceRoom(world);

            // 先に電柱+無限発電機（機械設置経路で確実に接続）。
            // Pole + generator first (machine-place path connects reliably).
            PlacePoleAndInfinityGenerator(world);

            // 2台（どちらも側壁に接しない床セル）→ V=73, S=108, A_total=13.7。
            // Two units on interior floor cells => V=73, S=108, A_total=13.7.
            var (filter1, filter2) = PlaceTwoAirFiltersWithOneFilterEach(world);

            // τ=73/10=7.3s → 75s(1500tick≈10τ) で平衡。
            // τ=7.3s; 75s (1500 ticks ≈ 10τ) reaches equilibrium.
            GameUpdater.RunFrames(1500);

            Assert.IsTrue(datastore.TryGetCleanRoomAt(InsideEmptyCellPos, out var room));
            Assert.AreEqual(73, room.Volume);
            Assert.AreEqual(108, room.SurfaceArea);

            // n·q が加算されていれば C_eq = 13.7/10 = 1.37。1台分(nq=5)なら 2.74 になり明確に区別できる。
            // If additive, C_eq=1.37; a non-additive bug (nq=5) would read 2.74 — clearly distinguishable.
            Assert.AreEqual(1.37, room.Concentration, 0.2, "n·q additive equilibrium");

            // 摩耗は同能力2台で等分配される。
            // Wear splits equally across two identical units.
            var w1 = filter1.GetComponent<CleanRoomAirFilterComponent>().WearProgress;
            var w2 = filter2.GetComponent<CleanRoomAirFilterComponent>().WearProgress;
            Assert.Greater(w1, 300.0, "unit1 wears");
            Assert.Greater(w2, 300.0, "unit2 wears");
            Assert.AreEqual(w1, w2, 1.0, "equal share for identical units");
        }

        // 内寸 5x5x3 を壁で囲い、境界壁2枚を ItemHatch / PipeHatch に差し替える（接続点2）。
        // Seal a 5x5x3 cavity; swap two boundary walls for ItemHatch / PipeHatch (connectors=2).
        private static void BuildReferenceRoom(IWorldBlockDatastore world)
        {
            for (var x = ShellMin.x; x <= ShellMax.x; x++)
            for (var y = ShellMin.y; y <= ShellMax.y; y++)
            for (var z = ShellMin.z; z <= ShellMax.z; z++)
            {
                var onShell = x == ShellMin.x || x == ShellMax.x ||
                              y == ShellMin.y || y == ShellMax.y ||
                              z == ShellMin.z || z == ShellMax.z;
                if (!onShell) continue;

                var pos = new Vector3Int(x, y, z);
                if (pos == ItemHatchPos || pos == PipeHatchPos) continue; // ハッチで差し替えるので壁は置かない
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, ItemHatchPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeHatchId, PipeHatchPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // 電柱と無限発電機を設置し、フィルターへ給電する（要求電力 ≤ infinityPower で満電）。
        // Place pole + infinite generator to power the filter(s) (required power <= infinityPower => full ratio).
        private static void PlacePoleAndInfinityGenerator(IWorldBlockDatastore world)
        {
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, GeneratorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // 2台のエアフィルターを置き、各台にフィルターを1個ずつ投入する。
        // Place two air filters and load one filter item into each.
        private static (IBlock filter1, IBlock filter2) PlaceTwoAirFiltersWithOneFilterEach(IWorldBlockDatastore world)
        {
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, AirFilterPos1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var filter1);
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, AirFilterPos2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var filter2);

            filter1.GetComponent<CleanRoomAirFilterItemComponent>()
                .InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 1));
            filter2.GetComponent<CleanRoomAirFilterItemComponent>()
                .InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 1));

            return (filter1, filter2);
        }
    }
}

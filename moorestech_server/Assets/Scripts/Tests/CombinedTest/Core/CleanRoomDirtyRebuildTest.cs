using System;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CleanRoom;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomDirtyRebuildTest
    {
        // 触れた壁AABB+1: 遠方の無関係な壁があってもリーク探索は局所bboxで即終了する。
        // Touched-wall AABB+1: leak search stays local even with distant unrelated walls.
        [Test]
        public void TouchedWallAabb_OpenStructureNearDistantWalls_LeaksCheaply()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 未密閉のコの字壁＋遠方(100セル先)に無関係な装飾壁の塊。
            // An unsealed U-shape plus an unrelated decorative wall cluster 100 cells away.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove); // 穴
            BuildWallShell(world, new Vector3Int(100, 0, 0), new Vector3Int(104, 4, 4));

            datastore.RebuildAll();

            // 穴あき側は部屋にならず、遠方の密閉部屋だけ成立。
            // The holed shell forms no room; only the distant sealed shell does.
            Assert.AreEqual(1, datastore.Rooms.Count);

            // 触れた壁AABB+1 なら、穴あき側のリーク探索は局所bbox脱出で即終了する。
            // With touched-wall AABB+1 the leak search exits the local bbox quickly.
            Assert.Less(datastore.LastRebuildVisitedCellCount, 1000,
                "leak search must be bounded by the touched-wall AABB, not the global AABB");
        }

        // 予算が小さいと2部屋は1tickで揃わず、繰り越し処理で最終的に揃う。
        // A small budget defers work across ticks; carry-over eventually finishes.
        [Test]
        public void DirtyBudget_TwoShells_AppearAcrossTicks_NotInOne()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 予算を「1tickに1シード相当」へ絞る。
            // Shrink the budget so one tick can finish only ~one seed.
            datastore.SetDirtyCellBudgetPerTickForTest(1);

            // 2つの離れた密閉シェルを同時に建てる（tickを挟まない）。
            // Build two separate sealed shells without ticking in between.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            BuildWallShell(world, new Vector3Int(20, 0, 0), new Vector3Int(24, 4, 4));

            // 予算1では1tickで両方は検出できない。
            // Budget 1 cannot finish both rooms in a single tick.
            GameUpdater.RunFrames(1);
            Assert.Less(datastore.Rooms.Count, 2, "budget must defer work to later ticks");

            // 繰り越し処理で最終的に両方検出される。
            // Carried-over seeds eventually detect both rooms.
            GameUpdater.RunFrames(2000);
            Assert.AreEqual(2, datastore.Rooms.Count, "all rooms appear eventually");
        }

        // 差分更新の核心: 遠方の変更では既存部屋のインスタンスも純度状態も維持される。
        // Essence of incremental update: a distant change keeps the existing room instance and purity state.
        [Test]
        public void DirtyIncremental_UntouchedRoomInstanceAndPuritySurvive()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            GameUpdater.RunFrames(50);
            Assert.AreEqual(1, datastore.Rooms.Count);
            var room = datastore.Rooms[0];

            // 保持帯 C=9 の平衡を作る（A=nq·C=5×9=45, N=9×27=243）→ 行1が持続する。
            // Stable hold-band C=9 (A=45, q=5, N=243) so threshold row 1 persists across ticks.
            datastore.AddAirFilter(new Vector3Int(2, 2, 2), new AirFilterStub(5.0));
            datastore.SetPollutionPerSecondProvider(_ => 45.0);
            room.AddImpurity(243.0);
            room.SetThresholdIndex(1);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, datastore.Rooms[0].ThresholdIndex, "sanity: row 1 holds at C=9");

            // 遠方に壁を1個置く → 差分更新では既存部屋に触れない。
            // Place a distant wall; incremental update must not touch the existing room.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(30, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(10);

            // 部屋インスタンスも純度状態もそのまま（差分更新の核心）。
            // Same instance, same purity state — the essence of incremental update.
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreSame(room, datastore.Rooms[0], "untouched room keeps its instance");
            Assert.AreEqual(243.0, datastore.Rooms[0].ImpurityCount, 1.0);
            Assert.AreEqual(1, datastore.Rooms[0].ThresholdIndex);
        }

        // テスト用フィルタースタブ。固定 q を返す。
        // Test stub filter returning a fixed q.
        private sealed class AirFilterStub : ICleanRoomAirFilter
        {
            public double RemovalVolumePerSecond { get; }
            public bool IsDestroy { get; private set; }
            public AirFilterStub(double q) { RemovalVolumePerSecond = q; }
            public void Destroy() { IsDestroy = true; }
        }

        // 1セルの壁を置くヘルパ。
        // Helper: place a single wall cell.
        private static void PlaceWall(IWorldBlockDatastore world, Vector3Int pos)
        {
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, pos,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // min..max の外殻だけ壁を置き、内部を空洞にするヘルパ。
        // Helper: place walls only on the shell of [min,max], leaving the interior hollow.
        private static void BuildWallShell(IWorldBlockDatastore world, Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var onShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!onShell) continue;
                PlaceWall(world, new Vector3Int(x, y, z));
            }
        }
    }
}

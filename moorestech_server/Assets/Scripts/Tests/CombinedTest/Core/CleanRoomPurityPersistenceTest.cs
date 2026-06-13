using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CleanRoom;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPurityPersistenceTest
    {
        [Test]
        public void SaveLoad_RoundTrip_PreservesImpurityAndThresholdIndex_AcrossFirstTick()
        {
            // 1. 保存側コンテナ: 部屋を作り N=1215（C=45, 行1の保持帯）・行1 を仕込んで JSON 化。
            // 1. Save-side container: build a room, seed N=1215 (C=45, row-1 hold band) and row 1.
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var saveWorld = ServerContext.WorldBlockDatastore;
            var saveDatastore = saveProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(saveWorld, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            saveDatastore.RebuildAll();
            var room = saveDatastore.Rooms[0];
            room.AddImpurity(1215.0);          // C = 1215/27 = 45（行1の保持帯 40〜50）
            room.SetThresholdIndex(1);

            var json = saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 2. 新規コンテナで Load → ブロック・部屋・純度を復元。
            // 2. Fresh container; Load restores blocks, rooms, and purity.
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loader = loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            loader.Load(json);

            var loadDatastore = loadProvider.GetService<CleanRoomDatastore>();
            Assert.AreEqual(1, loadDatastore.Rooms.Count, "room re-detected after load");
            Assert.AreEqual(1215.0, loadDatastore.Rooms[0].ImpurityCount, 1e-6, "N survived save/load");
            Assert.AreEqual(1, loadDatastore.Rooms[0].ThresholdIndex, "threshold row survived save/load");

            // 3. ロード直後の1tickで再々検出リセットが起きないこと（dirty残骸の非回帰・レビューA-1）。
            //    平衡条件（A_total=nq·C=5×45=225・q=5フィルター）を入れて C=45 を維持したままtickする。
            // 3. One tick after load must not wipe the state (stale-dirty regression, review A-1).
            var insideCell = new Vector3Int(2, 2, 2);
            loadDatastore.AddAirFilter(insideCell, new AirFilterStub(5.0));
            loadDatastore.SetPollutionPerSecondProvider(_ => 225.0);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(1215.0, loadDatastore.Rooms[0].ImpurityCount, 1.0, "N survives the first tick after load");
            Assert.AreEqual(1, loadDatastore.Rooms[0].ThresholdIndex,
                "hold-band row survives the first tick (would fall to row 2 if reset to Out)");
        }

        [Test]
        public void SaveLoad_DegradedOrphan_IsSavedAndRestored_WithRunningGrace()
        {
            // 猶予中（壁破壊直後）にセーブしても N が消えない（「猶予内再封でN継続」との整合）。
            // Saving during grace must not lose N (consistent with reseal-within-grace).
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var saveWorld = ServerContext.WorldBlockDatastore;
            var saveDatastore = saveProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(saveWorld, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            saveDatastore.RebuildAll();
            saveDatastore.Rooms[0].AddImpurity(150.0);

            saveWorld.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            saveDatastore.RebuildAll();
            GameUpdater.RunFrames(40); // 猶予 5.0 → 3.0 秒
            var json = saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            var loadDatastore = loadProvider.GetService<CleanRoomDatastore>();
            Assert.AreEqual(0, loadDatastore.Rooms.Count, "broken room is not detected");
            Assert.True(loadDatastore.TryGetDegradedOrphan(out var orphan), "Degraded orphan restored");
            Assert.AreEqual(150.0, orphan.ImpurityCount, 1e-6);
            Assert.AreEqual(3.0, orphan.GraceRemainingSeconds, 0.2, "grace keeps running across save/load");

            // 猶予内に再封 → N 復活。
            // Reseal within grace -> N recovers.
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId,
                new Vector3Int(2, 2, 0), BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            loadDatastore.RebuildAll();
            Assert.AreEqual(1, loadDatastore.Rooms.Count);
            Assert.AreEqual(150.0, loadDatastore.Rooms[0].ImpurityCount, 1e-6);
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

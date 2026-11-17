using Core.Update;
using Game.Block.Interface;
using Game.CleanRoom;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomIncrementalDetectionTest
    {
        [Test]
        public void PlaceAndRemoveWallUpdatesRoomsOverTicksTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 壁で箱を組み、数tick回すと部屋が現れる
            // Build a sealed box; the room appears after a few ticks
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));

            // 壁を1枚壊して数tick回すと部屋が消える
            // Remove one wall block; the room disappears after a few ticks
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove);
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(0, datastore.Rooms.Count);
            Assert.IsFalse(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));
        }

        [Test]
        public void SplitRoomCarriesThresholdRowTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // クラス判定を挟まない検出サービスで内寸3x1x1の部屋を作り、行B(1)を注入する
            // Build a 3x1x1-interior room with the detection service alone (no class decision) and inject row B (1)
            var service = new CleanRoomDetectionService(ServerContext.WorldBlockDatastore, CleanRoomDatastore.DirtyCellBudgetPerTick);
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 2));
            service.RebuildAll();
            Assert.AreEqual(1, service.Rooms.Count);
            service.Rooms[0].SetThresholdIndex(1);

            // 2部屋へ分割し両方が行を引き継ぐ
            // Placing a wall in the middle splits the room, and both rooms carry the original row
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 1, 1));
            service.OnBlockChanged(ServerContext.WorldBlockDatastore.GetOriginPosBlock(new Vector3Int(2, 1, 1)));
            service.ProcessDirtySeeds();
            Assert.AreEqual(2, service.Rooms.Count);
            Assert.AreEqual(1, FindRoom(service, new Vector3Int(1, 1, 1)).ThresholdIndex);
            Assert.AreEqual(1, FindRoom(service, new Vector3Int(3, 1, 1)).ThresholdIndex);
        }

        [Test]
        public void InteriorBlockPlacementKeepsRoomAndLookupWorksTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 内寸3x1x3の部屋を作る
            // Build a room with a 3x1x3 interior
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 4));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, datastore.Rooms.Count);

            // チェスト設置でVolumeだけ減る
            // An interior chest keeps a single room and only reduces Volume
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 1, 2));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(8, datastore.Rooms[0].Volume);

            // 全占有セルが同一部屋にあるブロックは部屋を引ける
            // A block whose occupied cells are all inside one room resolves to that room
            var chestBlock = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(2, 1, 2));
            Assert.IsTrue(datastore.TryGetCleanRoom(chestBlock, out var chestRoom));
            Assert.AreSame(datastore.Rooms[0], chestRoom);

            // 部屋の外の壁ブロック自体は部屋に属さない
            // A wall block itself does not belong to the room
            var wallBlock = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            Assert.IsFalse(datastore.TryGetCleanRoom(wallBlock, out _));
        }

        [Test]
        public void BudgetLimitsWorkPerTickButAlwaysProgressesTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // budget=1 で直接構築し、離れた2つの箱の全ブロックをdirty化する
            // Construct directly with budget=1 and mark every block of two distant boxes dirty
            var service = new CleanRoomDetectionService(ServerContext.WorldBlockDatastore, 1);
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            CleanRoomDetectionTest.BuildBox(new Vector3Int(10, 0, 0), new Vector3Int(12, 2, 2));
            foreach (var blockData in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                service.OnBlockChanged(blockData);

            // budget=1 では1tickで2部屋同時には検出できない
            // With budget=1 a single tick can never detect both rooms
            service.ProcessDirtySeeds();
            Assert.LessOrEqual(service.Rooms.Count, 1);

            // それでも毎tick最低1シードは前進し、いずれ両部屋が検出される
            // Still, each tick consumes at least one seed, so both rooms eventually appear
            var elapsedTicks = 0;
            while (service.Rooms.Count < 2 && elapsedTicks < 500)
            {
                service.ProcessDirtySeeds();
                elapsedTicks++;
            }
            Assert.AreEqual(2, service.Rooms.Count);
            Assert.Greater(elapsedTicks, 0);
        }

        [Test]
        public void ConcurrentBatchesEachPreserveTheirRoomsThresholdRowTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // budget=1（=1tickに1バッチ）で構築し、離れた内寸3x1x1の部屋を2つ検出する
            // Construct with budget=1 (one batch per tick) and detect two distant 3x1x1 rooms
            var service = new CleanRoomDetectionService(ServerContext.WorldBlockDatastore, 1);
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 2));
            CleanRoomDetectionTest.BuildBox(new Vector3Int(10, 0, 0), new Vector3Int(14, 2, 2));
            service.RebuildAll();
            Assert.AreEqual(2, service.Rooms.Count);

            // 各部屋へ別々の行を注入する（セル包含で引き当て、リスト順には依存しない）
            // Inject a distinct row into each room, looked up by cell containment, never by list order
            FindRoom(service, new Vector3Int(1, 1, 1)).SetThresholdIndex(1);
            FindRoom(service, new Vector3Int(11, 1, 1)).SetThresholdIndex(2);

            // 同一tick内で両部屋の中央に壁を置き分割する。2バッチが同時に保留される
            // Split both rooms with a central wall in the same tick, leaving two batches pending
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 1, 1));
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(12, 1, 1));
            service.OnBlockChanged(ServerContext.WorldBlockDatastore.GetOriginPosBlock(new Vector3Int(2, 1, 1)));
            service.OnBlockChanged(ServerContext.WorldBlockDatastore.GetOriginPosBlock(new Vector3Int(12, 1, 1)));

            // 1tick目はAのバッチだけ処理
            // Tick 1: only room A's batch is processed whole, and both sub-rooms carry its row
            service.ProcessDirtySeeds();
            Assert.AreEqual(1, FindRoom(service, new Vector3Int(1, 1, 1)).ThresholdIndex);
            Assert.AreEqual(1, FindRoom(service, new Vector3Int(3, 1, 1)).ThresholdIndex);
            Assert.AreEqual(2, FindRoom(service, new Vector3Int(11, 1, 1)).ThresholdIndex);

            // 2tick目：部屋Bのバッチが処理される。Aは既検出のまま維持される
            // Tick 2: room B's batch is processed while room A stays as already detected
            service.ProcessDirtySeeds();
            Assert.AreEqual(4, service.Rooms.Count);

            // 各元部屋の行が保たれる
            // No sub-room resets to Out; each original room's row is preserved
            Assert.AreEqual(1, FindRoom(service, new Vector3Int(1, 1, 1)).ThresholdIndex);
            Assert.AreEqual(1, FindRoom(service, new Vector3Int(3, 1, 1)).ThresholdIndex);
            Assert.AreEqual(2, FindRoom(service, new Vector3Int(11, 1, 1)).ThresholdIndex);
            Assert.AreEqual(2, FindRoom(service, new Vector3Int(13, 1, 1)).ThresholdIndex);
        }

        private static global::Game.CleanRoom.CleanRoom FindRoom(CleanRoomDetectionService service, Vector3Int cell)
        {
            foreach (var room in service.Rooms)
                if (room.Contains(cell))
                    return room;

            Assert.Fail($"No room contains cell {cell}");
            return null;
        }
    }
}

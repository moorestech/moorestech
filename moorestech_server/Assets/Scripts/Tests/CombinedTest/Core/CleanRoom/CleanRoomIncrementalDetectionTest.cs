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
        public void SplitRoomRedistributesImpurityTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 内寸3x1x1の部屋を作り N=90 を注入する
            // Build a room with a 3x1x1 interior and inject N=90
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 2));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, datastore.Rooms.Count);
            datastore.Rooms[0].SetImpurity(90.0);

            // 中央に壁を置いて2部屋へ分割すると N がセル重なりで按分される
            // Placing a wall in the middle splits the room and redistributes N by cell overlap
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 1, 1));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(2, datastore.Rooms.Count);

            // 部屋リストの順序は非決定的なため、両部屋とも30.0であることを検証する
            // Room list order is non-deterministic, so assert both rooms hold 30.0
            foreach (var room in datastore.Rooms) Assert.AreEqual(30.0, room.ImpurityCount, 0.001);
        }

        [Test]
        public void InteriorBlockPlacementCarriesImpurityAndLookupWorksTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 内寸3x1x3の部屋を作り N=45 を注入する
            // Build a room with a 3x1x3 interior and inject N=45
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 4));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, datastore.Rooms.Count);
            datastore.Rooms[0].SetImpurity(45.0);

            // 内部にチェストを置くと Volume が減るが Cells 全重なりで N は維持される
            // An interior chest reduces Volume, but full cell overlap keeps N intact
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 1, 2));
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(8, datastore.Rooms[0].Volume);
            Assert.AreEqual(45.0, datastore.Rooms[0].ImpurityCount, 0.001);

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
        public void ConcurrentBatchesEachPreserveTheirRoomsImpurityTest()
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

            // 各部屋へ既知のNを注入する（セル包含で引き当て、リスト順には依存しない）
            // Inject known N into each room, looked up by cell containment, never by list order
            FindRoom(service, new Vector3Int(1, 1, 1)).SetImpurity(90.0);
            FindRoom(service, new Vector3Int(11, 1, 1)).SetImpurity(60.0);

            // 同一tick内で両部屋の中央に壁を置き分割する。2バッチが同時に保留される
            // Split both rooms with a central wall in the same tick, leaving two batches pending
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 1, 1));
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(12, 1, 1));
            service.OnBlockChanged(ServerContext.WorldBlockDatastore.GetOriginPosBlock(new Vector3Int(2, 1, 1)));
            service.OnBlockChanged(ServerContext.WorldBlockDatastore.GetOriginPosBlock(new Vector3Int(12, 1, 1)));

            // 1tick目：部屋Aのバッチだけが丸ごと処理され、両サブ部屋へNが按分される
            // Tick 1: only room A's batch is processed whole, splitting N across both sub-rooms
            service.ProcessDirtySeeds();
            Assert.AreEqual(30.0, FindRoom(service, new Vector3Int(1, 1, 1)).ImpurityCount, 0.001);
            Assert.AreEqual(30.0, FindRoom(service, new Vector3Int(3, 1, 1)).ImpurityCount, 0.001);
            Assert.AreEqual(60.0, FindRoom(service, new Vector3Int(11, 1, 1)).ImpurityCount, 0.001);

            // 2tick目：部屋Bのバッチが処理される。Aは既検出のまま維持される
            // Tick 2: room B's batch is processed while room A stays as already detected
            service.ProcessDirtySeeds();
            Assert.AreEqual(4, service.Rooms.Count);

            // どのサブ部屋もN=0へリセットされず、各元部屋の残存セル分のNが保存される
            // No sub-room resets to N=0; each original room preserves N over its surviving cells
            Assert.AreEqual(30.0, FindRoom(service, new Vector3Int(1, 1, 1)).ImpurityCount, 0.001);
            Assert.AreEqual(30.0, FindRoom(service, new Vector3Int(3, 1, 1)).ImpurityCount, 0.001);
            Assert.AreEqual(20.0, FindRoom(service, new Vector3Int(11, 1, 1)).ImpurityCount, 0.001);
            Assert.AreEqual(20.0, FindRoom(service, new Vector3Int(13, 1, 1)).ImpurityCount, 0.001);
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

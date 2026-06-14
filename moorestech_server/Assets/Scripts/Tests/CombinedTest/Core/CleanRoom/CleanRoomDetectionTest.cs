using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
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
    public class CleanRoomDetectionTest
    {
        [Test]
        public void PlaceBoundaryBlock_HasKindedBoundaryComponent()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatch);

            Assert.True(hatch.TryGetComponent<ICleanRoomBoundaryComponent>(out var marker));
            Assert.AreEqual(CleanRoomBoundaryKind.ItemHatch, marker.BoundaryKind);
        }

        [Test]
        public void Detect_SealedShell_ReturnsOneRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 3x3x3 の外殻を壁で作る。中心 (1,1,1) だけ空洞。
            // Build a 3x3x3 wall shell; only the center cell (1,1,1) is hollow.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));

            var rooms = CleanRoomDetector.DetectAllRooms(world);

            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(1, rooms[0].Volume, "Inner volume should be 1 cell");
            Assert.AreEqual(1, rooms[0].Cells.Count);
            Assert.AreEqual(6, rooms[0].SurfaceArea, "A single cell touches 6 wall faces");
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

        // 既存ブロックを除去して別のブロックIDに置き換えるヘルパ。
        // Helper: remove an existing block and replace it with the given block id.
        private static void ReplaceWith(IWorldBlockDatastore world, Vector3Int pos, BlockId blockId)
        {
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            world.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // Step 1: 面に穴があるシェルは密閉されないため部屋が検出されない。
        // Step 1: A shell with a missing face block is not sealed, so no room is returned.
        [Test]
        public void Detect_ShellWithHole_ReturnsNoRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove); // 面に穴

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "A shell with a hole must not form a sealed room");
        }

        // Step 2: 5x5x5 シェルの内寸 3x3x3=27、表面積 54 を確認する。
        // Step 2: A 5x5x5 shell yields interior 3x3x3=27 volume and surface area 54.
        [Test]
        public void Detect_5x5x5Shell_HasVolume27Surface54()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // 内部 3x3x3

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(27, rooms[0].Volume);
            Assert.AreEqual(54, rooms[0].SurfaceArea); // 3x3面 x6 = 54
        }

        // Step 3: 離れた2つの密閉シェルは独立した2部屋として検出される。
        // Step 3: Two separate sealed shells are detected as two independent rooms.
        [Test]
        public void Detect_TwoSeparateShells_ReturnsTwoRooms()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            BuildWallShell(world, new Vector3Int(10, 0, 0), new Vector3Int(12, 2, 2));

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count);
        }

        // Step 4: ドア/アイテム/パイプハッチも境界ブロックとして密閉に機能する。
        // Step 4: Door, item, and pipe hatches all function as boundary blocks for sealing.
        [Test]
        public void Detect_DoorHatchItemHatchPipeHatch_AlsoSeal()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            ReplaceWith(world, new Vector3Int(1, 1, 0), ForUnitTestModBlockId.CleanRoomDoorHatchId);
            ReplaceWith(world, new Vector3Int(1, 1, 2), ForUnitTestModBlockId.CleanRoomItemHatchId);
            ReplaceWith(world, new Vector3Int(0, 1, 1), ForUnitTestModBlockId.CleanRoomPipeHatchId);

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count, "DoorHatch/ItemHatch/PipeHatch must seal the room");
            Assert.AreEqual(1, rooms[0].Volume);
        }

        // Step 5: 壁の穴に非境界ブロックを置いても部屋は成立しない。
        // Step 5: Placing a non-boundary block in a hole still does not seal the room.
        [Test]
        public void Detect_NonBoundaryBlockInHole_DoesNotSeal()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove);
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 1, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "Only boundary blocks seal; a normal block does not");
        }

        // Step 6: 室内の非境界ブロックは Cells に含まれるが Volume には計上されない。
        // Step 6: An interior non-boundary block is included in Cells but excluded from Volume.
        [Test]
        public void Detect_InteriorBlock_ExcludedFromVolume_IncludedInCells()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 1, 1),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(27, rooms[0].Cells.Count, "Cells includes occupied interior cells");
            Assert.AreEqual(26, rooms[0].Volume, "Occupied cells are excluded from V");
            Assert.AreEqual(51, rooms[0].SurfaceArea, "3 wall faces of the occupied corner cell leave S");
            Assert.True(rooms[0].Contains(new Vector3Int(1, 1, 1)), "Occupied cell still belongs to the room");
        }

        // Step 7: エッジのみ接触する斜め壁は 6 近傍充填では気密と見なされる。
        // Step 7: Diagonal walls touching only at edges are airtight under 6-neighbor flood fill.
        [Test]
        public void Detect_DiagonalWalls_AreAirtight()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            for (var x = 0; x <= 4; x++)
            for (var y = 0; y <= 4; y++)
            {
                PlaceWall(world, new Vector3Int(x, y, 0));
                PlaceWall(world, new Vector3Int(x, y, 2));
                if (x == 0 || x == 4 || y == 0 || y == 4) PlaceWall(world, new Vector3Int(x, y, 1));
            }
            PlaceWall(world, new Vector3Int(1, 3, 1));
            PlaceWall(world, new Vector3Int(2, 2, 1));
            PlaceWall(world, new Vector3Int(3, 1, 1));

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count, "Edge-only diagonal walls are airtight under 6-neighbor fill");
            Assert.AreEqual(3, rooms[0].Volume);
            Assert.AreEqual(3, rooms[1].Volume);
        }

        // Step 8: 内部パーティション壁があると2つの独立した部屋に分割される。
        // Step 8: A full interior partition wall splits the enclosure into two separate rooms.
        [Test]
        public void Detect_InternalPartition_SplitsIntoTwoRooms()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            for (var y = 1; y <= 3; y++)
            for (var z = 1; z <= 3; z++)
                PlaceWall(world, new Vector3Int(2, y, z));

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count, "A full interior partition splits the room");
            Assert.AreEqual(9, rooms[0].Volume);
            Assert.AreEqual(9, rooms[1].Volume);
        }

        // Step 9: パーティション壁を1セル除去すると2部屋が結合して1部屋になる。
        // Step 9: Removing a single partition cell merges the two rooms into one.
        [Test]
        public void Detect_RemovePartitionWall_MergesIntoOneRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            for (var y = 1; y <= 3; y++)
            for (var z = 1; z <= 3; z++)
                PlaceWall(world, new Vector3Int(2, y, z));
            world.RemoveBlock(new Vector3Int(2, 2, 2), BlockRemoveReason.ManualRemove);

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count, "Removing a partition cell merges the two rooms");
            Assert.AreEqual(19, rooms[0].Volume);
        }

        // Step 10: 2部屋の共有壁にあるハッチは境界として機能し、部屋を隔離する。
        // Step 10: A hatch on the shared wall acts as a boundary and keeps the rooms isolated.
        [Test]
        public void Detect_HatchOnSharedWall_KeepsRoomsIsolated()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            BuildWallShell(world, new Vector3Int(2, 0, 0), new Vector3Int(4, 2, 2));
            ReplaceWith(world, new Vector3Int(2, 1, 1), ForUnitTestModBlockId.CleanRoomItemHatchId);

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count, "A hatch on a shared wall isolates (never connects) the rooms");
        }

        // Step 11: MaxRoomVolume を超える内寸では部屋が成立しない。
        // Step 11: Interiors exceeding MaxRoomVolume cells do not form a valid room.
        [Test]
        public void Detect_VolumeOverMax_DoesNotFormRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 内寸 17x16x16 = 4352 セル > MaxRoomVolume(4096)
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(18, 17, 17));

            var rooms = CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "Rooms exceeding MaxRoomVolume cells must not form");
        }

        // Step 12: 境界ブロックの設置/除去が CleanRoomDatastore に反映される。
        // Step 12: Placing/removing a boundary block is reflected in CleanRoomDatastore.
        [Test]
        public void Datastore_PlaceThenBreakBoundary_UpdatesRooms()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, datastore.Rooms.Count);

            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, datastore.Rooms.Count);
        }

        // Step 13: 部屋外の非境界ブロックの設置は再検出をトリガしない。
        // Step 13: Placing a non-boundary block far outside rooms must not trigger re-detection.
        [Test]
        public void Datastore_FarNonBoundaryPlacement_DoesNotRebuild()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);
            var before = datastore.RebuildCount;

            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(50, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(before, datastore.RebuildCount, "Non-boundary placement outside rooms must not trigger re-detection");
        }

        // Step 14: 部屋内の非境界ブロック設置は再検出され Volume が減少する。
        // Step 14: Placing a non-boundary block inside the room triggers rebuild and reduces Volume.
        [Test]
        public void Datastore_InteriorBlockPlacement_RebuildsAndUpdatesVolume()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(27, datastore.Rooms[0].Volume);

            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(26, datastore.Rooms[0].Volume, "Interior occupied cell is removed from V");
        }

        // Step 15: TryGetCleanRoomAt は Cells 内セルを含む部屋を返し、境界セルは false。
        // Step 15: TryGetCleanRoomAt returns the room containing the cell; boundary cells return false.
        [Test]
        public void Datastore_TryGetCleanRoomAt_ReturnsContainingRoom()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);

            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out _));
            Assert.False(datastore.TryGetCleanRoomAt(new Vector3Int(50, 50, 50), out _));
            Assert.False(datastore.TryGetCleanRoomAt(new Vector3Int(0, 0, 0), out _), "Boundary cells belong to no room");
        }

        // Step 16: 室内ブロックは部屋に帰属し、境界ブロック（壁）は帰属しない。
        // Step 16: Interior blocks belong to a room; boundary blocks (walls) do not.
        // NOTE: マルチセルブロック（2x2x2等）の「全セル同一部屋」「2部屋またがり=false」は
        // テスト用の多セル非境界ブロックが存在する後続フェーズに繰り越す。
        // NOTE: Multi-block "all cells same room" / "spanning two rooms = false" coverage is deferred
        // to a later phase when a multi-cell non-boundary test block exists.
        [Test]
        public void Datastore_TryGetCleanRoom_InteriorBlockBelongs_BoundaryDoesNot()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var inside);
            GameUpdater.RunFrames(1);

            // 占有セルは V から除外されるが Cells には含まれるため、室内ブロックは部屋に帰属する。
            // Occupied cells are excluded from V but kept in Cells, so interior blocks belong to the room.
            Assert.True(datastore.TryGetCleanRoom(inside, out _), "A block fully inside the room is contained");

            // 境界ブロック（壁）は Cells 外＝帰属しない。境界用クエリは後続フェーズの GetAdjacentCleanRooms。
            var wall = world.GetBlock(new Vector3Int(0, 0, 0));
            Assert.False(datastore.TryGetCleanRoom(wall, out _), "Boundary blocks never belong to a room's Cells");
        }
    }
}

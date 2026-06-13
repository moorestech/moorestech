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
    }
}

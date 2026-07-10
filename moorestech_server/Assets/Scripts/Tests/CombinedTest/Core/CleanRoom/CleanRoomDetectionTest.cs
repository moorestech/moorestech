using System;
using Core.Master;
using Game.Block.Interface;
using Game.CleanRoom;
using Game.CleanRoom.Util;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomDetectionTest
    {
        [Test]
        public void SealedRoomIsDetectedTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 内寸1x1x1（外形3x3x3）の箱を壁で組む
            // Build a 3x3x3 shell enclosing a single interior cell
            BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));

            var rooms = CleanRoomDetector.DetectAllRooms(ServerContext.WorldBlockDatastore, out _);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(1, rooms[0].Volume);
            Assert.AreEqual(6, rooms[0].SurfaceArea);
            Assert.IsTrue(rooms[0].Contains(new Vector3Int(1, 1, 1)));
        }

        [Test]
        public void MissingWallLeaksTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            // 壁を1枚壊す → リークして部屋不成立
            // Remove one wall block; the fill leaks and no room forms
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove);

            var rooms = CleanRoomDetector.DetectAllRooms(ServerContext.WorldBlockDatastore, out _);
            Assert.AreEqual(0, rooms.Count);
        }

        [Test]
        public void InteriorMachineOccupiedCellReducesVolumeTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 内寸3x1x3（外形5x3x5）。内部にチェストを1個置くと V が1減り Cells には残る
            // Interior 3x1x3; an interior chest reduces V by one but stays inside Cells
            BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 4));
            AddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 1, 2));

            var rooms = CleanRoomDetector.DetectAllRooms(ServerContext.WorldBlockDatastore, out _);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(8, rooms[0].Volume);       // 9 - 1
            Assert.IsTrue(rooms[0].Contains(new Vector3Int(2, 1, 2)));
        }

        #region TestHelper

        // min..max の外殻に壁ブロックを設置する（内部は空洞）
        // Place wall blocks on the shell of min..max, leaving the interior hollow
        public static void BuildBox(Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var isShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (isShell) AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(x, y, z));
            }
        }

        public static void AddBlock(BlockId blockId, Vector3Int pos)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        #endregion
    }
}

using System;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomPanelOrientationTest
    {
        [Test]
        public void MixedOrientationShellSealsAndGetsClassTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();

            // 外殻(0..6,0..4,0..6)の各セルへ、12向きを順に割り当てて境界ブロックを置く
            // Place boundary blocks on the shell (0..6,0..4,0..6), cycling through all 12 directions
            var directions = (BlockDirection[])Enum.GetValues(typeof(BlockDirection));
            var placedCount = 0;
            for (var x = 0; x <= 6; x++)
            for (var y = 0; y <= 4; y++)
            for (var z = 0; z <= 6; z++)
            {
                if (x != 0 && x != 6 && y != 0 && y != 4 && z != 0 && z != 6) continue;
                var direction = directions[placedCount % directions.Length];
                var placed = ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(x, y, z), direction, Array.Empty<BlockCreateParam>(), out _);
                Assert.IsTrue(placed, $"boundary block at ({x},{y},{z}) facing {direction} was not placed");
                placedCount++;
            }

            // 清浄機1台にフィルターを入れ、満電で十分に回す
            // Load filters into one purifier and run it long enough at full power
            var airFilter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(3, 1, 3));
            airFilter.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 5);
            ElectricWireTestUtil.WirePower(airFilter.BlockPositionInfo.OriginalPos, new Vector3Int(30, 0, 30), 100f);
            for (var i = 0; i < 2000; i++) GameUpdater.UpdateOneTick();

            // 向きに関係なく北向きだけの部屋と同じ体積・表面積になり、最上位のクラスに入る
            // Regardless of directions the room matches the all-north room in volume and surface, reaching the top class
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room), "mixed-orientation shell was not detected as a room");
            Assert.AreEqual(74, room.Volume);
            Assert.AreEqual(109, room.SurfaceArea);
            Assert.AreEqual(0, room.ThresholdIndex);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ConveyorOverpass
{
    // 地形を下限として乗り越えるベルト列（ADR 0081）の高さプロファイルを検証する
    // Verifies the height profile of a belt run that climbs over terrain as a lower bound (ADR 0081)
    public class ConveyorOverpassRaiserTerrainTest
    {
        [Test]
        public void HillInMiddle_ClimbsOverAndComesBack()
        {
            var infos = FlatEastPath(7);
            var result = Raise(infos, Terrain((3, 1), (4, 1)));

            Assert.AreEqual(new[] { 0, 0, 0, 1, 1, 0, 0 }, Ys(infos));
            Assert.AreEqual(BlockVerticalDirection.Up, infos[2].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Down, infos[5].VerticalDirection);
            Assert.IsFalse(result.TerrainClimbAbandoned);
            Assert.IsTrue(infos.All(info => info.Placeable));
        }

        [Test]
        public void Valley_DoesNotDescend()
        {
            var infos = FlatEastPath(5);
            var result = Raise(infos, Terrain((1, -3), (2, -3), (3, -3)));

            Assert.AreEqual(new[] { 0, 0, 0, 0, 0 }, Ys(infos));
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        [Test]
        public void HorizontalShape_IsKept()
        {
            var infos = FlatEastPath(7);
            Raise(infos, Terrain((3, 2)));

            for (var x = 0; x < 7; x++) Assert.AreEqual(new Vector2Int(x, 0), new Vector2Int(infos[x].Position.x, infos[x].Position.z));
        }

        [Test]
        public void TwoHillsNarrowGap_BridgedLikeBlockOverpass()
        {
            var infos = FlatEastPath(7);
            Raise(infos, Terrain((2, 1), (4, 1)));

            Assert.AreEqual(new[] { 0, 0, 1, 1, 1, 0, 0 }, Ys(infos));
        }

        [Test]
        public void SteepNextToEndpoint_FallsBackToTerrainFreePathAndNotifies()
        {
            var infos = FlatEastPath(3);
            var result = Raise(infos, Terrain((1, 2)));

            Assert.AreEqual(new[] { 0, 0, 0 }, Ys(infos));
            Assert.IsTrue(infos.All(info => info.Placeable));
            Assert.IsTrue(result.OverpassBlocked.All(blocked => !blocked));
            Assert.IsTrue(result.TerrainClimbAbandoned);
        }

        [Test]
        public void FallbackStillUnsolvable_KeepsBlockInfeasibilityWithoutNotice()
        {
            var infos = FlatEastPath(3);
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0), new(1, 1, 0) };
            var result = new ConveyorOverpassRaiser().Raise(infos, 2, occupied.Contains, Terrain((1, 2)));

            Assert.IsFalse(infos[0].Placeable);
            Assert.IsFalse(infos[2].Placeable);
            Assert.AreEqual(new[] { true, false, true }, result.OverpassBlocked);
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        [Test]
        public void EndpointTerrain_IsIgnored()
        {
            var infos = FlatEastPath(5);
            var result = Raise(infos, Terrain((0, 3), (4, 3)));

            Assert.AreEqual(new[] { 0, 0, 0, 0, 0 }, Ys(infos));
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        [Test]
        public void BlockOnHill_IsScannedFromLiftedBase()
        {
            var infos = FlatEastPath(7);
            var occupied = new HashSet<Vector3Int> { new(3, 1, 0) };
            new ConveyorOverpassRaiser().Raise(infos, 6, occupied.Contains, Terrain((3, 1)));

            Assert.AreEqual(new[] { 0, 0, 1, 2, 1, 0, 0 }, Ys(infos));
        }

        [Test]
        public void CornerOnHill_KeepsCornerPlateau()
        {
            var infos = FlatEastPath(5);
            new ConveyorOverpassRaiser().Raise(infos, 2, _ => false, Terrain((2, 1)));

            Assert.AreEqual(new[] { 0, 1, 1, 1, 0 }, Ys(infos));
        }

        [Test]
        public void NoGroundCell_GetsNoLowerBound()
        {
            var infos = FlatEastPath(5);
            var result = Raise(infos, Terrain());

            Assert.AreEqual(new[] { 0, 0, 0, 0, 0 }, Ys(infos));
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        #region Helpers

        private static ConveyorOverpassRaiseResult Raise(List<PlaceInfo> infos, FakeConveyorTerrainFloor terrain)
        {
            return new ConveyorOverpassRaiser().Raise(infos, infos.Count - 1, _ => false, terrain);
        }

        private static FakeConveyorTerrainFloor Terrain(params (int x, int floorY)[] cells)
        {
            return new FakeConveyorTerrainFloor(cells.ToDictionary(cell => new Vector2Int(cell.x, 0), cell => cell.floorY));
        }

        private static int[] Ys(List<PlaceInfo> infos)
        {
            return infos.Select(info => info.Position.y).ToArray();
        }

        private static List<PlaceInfo> FlatEastPath(int length)
        {
            // 原点から+X方向へ水平に並ぶベルト列
            // A horizontal belt row extending in +X from the origin
            var list = new List<PlaceInfo>();
            for (var x = 0; x < length; x++)
            {
                list.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, 0, 0),
                    Direction = BlockDirection.East,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                });
            }
            return list;
        }

        #endregion
    }
}

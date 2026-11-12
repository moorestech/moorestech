using Client.Game.InGame.Block.Index;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class PlacementAimCellTracerTest
    {
        private static readonly Vector3Int Origin = new(10, 5, 20);
        private static readonly Vector3Int Size = new(2, 1, 3);

        [TestCase(BlockDirection.North)]
        [TestCase(BlockDirection.East)]
        public void OutsideRays_ResolveWorldFaces(BlockDirection direction)
        {
            var (cells, posInfo) = CreateSingle(direction);
            var c = Center(posInfo);

            AssertFace(cells, c + Vector3.right * 20, Vector3.left, PreviewSurfaceType.YZ_X);
            AssertFace(cells, c + Vector3.left * 20, Vector3.right, PreviewSurfaceType.YZ_Origin);
            AssertFace(cells, c + Vector3.up * 20, Vector3.down, PreviewSurfaceType.XZ_Y);
            AssertFace(cells, c + Vector3.down * 20, Vector3.up, PreviewSurfaceType.XZ_Origin);
            AssertFace(cells, c + Vector3.forward * 20, Vector3.back, PreviewSurfaceType.YX_Z);
            AssertFace(cells, c + Vector3.back * 20, Vector3.forward, PreviewSurfaceType.YX_Origin);

            // 上から撃ったときの距離は箱の上面まで
            // Shooting from above measures the distance to the top face
            var fromTop = new Ray(c + Vector3.up * 20, Vector3.down);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, fromTop, 100f, out var distance, out _));
            Assert.AreEqual(c.y + 20 - (posInfo.MaxPos.y + 1), distance, 1e-4f);
        }

        [Test]
        public void InsideStart_HitsExitFace()
        {
            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var c = Center(posInfo);

            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, new Ray(c, Vector3.right), 100f, out var distance, out var surface));
            Assert.AreEqual(PreviewSurfaceType.YZ_X, surface);
            Assert.AreEqual(posInfo.MaxPos.x + 1 - c.x, distance, 1e-4f);
        }

        [Test]
        public void MaxDistanceBeforeBlock_Misses()
        {
            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var c = Center(posInfo);
            var toTop = c.y + 20 - (posInfo.MaxPos.y + 1);

            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, new Ray(c + Vector3.up * 20, Vector3.down), toTop - 0.01f, out _, out _));
        }

        [Test]
        public void NearerBlockWins()
        {
            var cells = new BlockOccupiedCellIndex();
            cells.Register(new BlockPositionInfo(new Vector3Int(10, 5, 20), BlockDirection.North, Vector3Int.one));
            cells.Register(new BlockPositionInfo(new Vector3Int(20, 5, 20), BlockDirection.North, Vector3Int.one));

            var ray = new Ray(new Vector3(30.5f, 5.5f, 20.5f), Vector3.left);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out var distance, out var surface));
            Assert.AreEqual(30.5f - 21f, distance, 1e-4f);
            Assert.AreEqual(PreviewSurfaceType.YZ_X, surface);
        }

        [Test]
        public void EdgeGraze_PrefersY()
        {
            var cells = new BlockOccupiedCellIndex();
            cells.Register(new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));

            var ray = new Ray(new Vector3(3f, 3f, 0.5f), new Vector3(-1f, -1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out _, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, surface);
        }

        [Test]
        public void MissOrEmpty_ReturnsFalse()
        {
            var empty = new BlockOccupiedCellIndex();
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(empty, new Ray(Vector3.up * 10, Vector3.down), 100f, out _, out _));

            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var above = Center(posInfo) + Vector3.up * 10 + Vector3.left * 20;
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, new Ray(above, Vector3.right), 100f, out _, out _));
        }

        [Test]
        public void NoMaxDistance_StopsAtBounds()
        {
            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var above = Center(posInfo) + Vector3.up * 10 + Vector3.left * 20;
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, new Ray(above, Vector3.right), float.PositiveInfinity, out _, out _));
        }

        private static (BlockOccupiedCellIndex, BlockPositionInfo) CreateSingle(BlockDirection direction)
        {
            var cells = new BlockOccupiedCellIndex();
            var posInfo = new BlockPositionInfo(Origin, direction, Size);
            cells.Register(posInfo);
            return (cells, posInfo);
        }

        private static Vector3 Center(BlockPositionInfo posInfo)
        {
            return ((Vector3)posInfo.MinPos + posInfo.MaxPos + Vector3.one) / 2f;
        }

        private static void AssertFace(BlockOccupiedCellIndex cells, Vector3 from, Vector3 direction, PreviewSurfaceType expected)
        {
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, new Ray(from, direction), 100f, out _, out var surface), $"from {from}");
            Assert.AreEqual(expected, surface, $"from {from}");
        }
    }
}

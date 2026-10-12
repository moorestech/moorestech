using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class PlacementRunPositionCalculatorTest
    {
        [Test]
        public void X軸へ刻み幅で伸びる()
        {
            var run = PlacementRunPositionCalculator.Calculate(new Vector3Int(6, 32, 6), new Vector3Int(10, 32, 6), Vector3Int.one);
            Assert.AreEqual(5, run.Positions.Count);
            Assert.AreEqual(PlacementRunAxis.X, run.Axis);
            Assert.AreEqual(4, run.CursorIndex);
        }

        [Test]
        public void 刻み幅3では割り切れない終点を末尾で代替する()
        {
            var run = PlacementRunPositionCalculator.Calculate(Vector3Int.zero, new Vector3Int(0, 0, 7), new Vector3Int(3, 1, 3));
            Assert.AreEqual(3, run.Positions.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 6), run.Positions[2]);
            Assert.AreEqual(2, run.CursorIndex);
        }

        [Test]
        public void 始点と終点が同じなら1点()
        {
            var run = PlacementRunPositionCalculator.Calculate(Vector3Int.one, Vector3Int.one, new Vector3Int(2, 2, 2));
            Assert.AreEqual(1, run.Positions.Count);
            Assert.AreEqual(0, run.CursorIndex);
        }
    }
}

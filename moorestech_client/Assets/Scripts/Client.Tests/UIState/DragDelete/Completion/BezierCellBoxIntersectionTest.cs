using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete.Completion
{
    public class BezierCellBoxIntersectionTest
    {
        [Test]
        public void CrossingOnlyBetweenOldSamplePointsIsSelected()
        {
            var curve = new BezierCellBoxIntersection(new Vector3(-1000, .5f, .5f), new Vector3(-333, .5f, .5f), new Vector3(333, .5f, .5f), new Vector3(1000, .5f, .5f));
            Assert.IsTrue(curve.Intersects(Vector3Int.zero, Vector3Int.zero));
        }

        [Test]
        public void LowerFaceTangencyIsIncluded()
        {
            var curve = Vertical(-3, 1, 1, -3);
            Assert.IsTrue(curve.Intersects(Vector3Int.zero, Vector3Int.zero));
        }

        [Test]
        public void UpperFaceTangencyIsExcluded()
        {
            var curve = Vertical(4, 0, 0, 4);
            Assert.IsFalse(curve.Intersects(Vector3Int.zero, Vector3Int.zero));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(-1, false)]
        public void ConstantCurveRespectsHalfOpenBounds(float y, bool expected)
        {
            Assert.AreEqual(expected, Vertical(y, y, y, y).Intersects(Vector3Int.zero, Vector3Int.zero));
        }

        [Test]
        public void NegativeCellAndRepeatedQueriesUseTheCurrentBox()
        {
            var curve = new BezierCellBoxIntersection(new Vector3(-10, -1.5f, -1.5f), new Vector3(-8, -1.5f, -1.5f), new Vector3(-4, -1.5f, -1.5f), new Vector3(-1, -1.5f, -1.5f));
            Assert.IsTrue(curve.Intersects(new Vector3Int(-5, -2, -2), new Vector3Int(-5, -2, -2)));
            Assert.IsFalse(curve.Intersects(Vector3Int.zero, Vector3Int.zero));
        }

        private static BezierCellBoxIntersection Vertical(float p0, float p1, float p2, float p3)
        {
            return new BezierCellBoxIntersection(new Vector3(.5f, p0, .5f), new Vector3(.5f, p1, .5f), new Vector3(.5f, p2, .5f), new Vector3(.5f, p3, .5f));
        }
    }
}

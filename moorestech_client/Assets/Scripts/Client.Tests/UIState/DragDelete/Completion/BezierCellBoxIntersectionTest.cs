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

        [Test]
        public void NonBinaryUpperTangenciesStayOutsideWhileLowerTangenciesAndNarrowCrossingsEnter()
        {
            for (var q = 1; q <= 30; q++)
            {
                // y=1+(3qt-1)^2 は t=1/(3q) で上面にだけ接する
                // y=1+(3qt-1)^2 touches only the upper face at t=1/(3q)
                var outside = Vertical(2, 2-2*q, 3*q*q+2-4*q, 9*q*q-6*q+2);
                Assert.IsFalse(outside.Intersects(Vector3Int.zero, Vector3Int.zero), $"upper q={q}");
                var lower = Vertical(-1, 2*q-1, -3*q*q+4*q-1, -9*q*q+6*q-1);
                Assert.IsTrue(lower.Intersects(Vector3Int.zero, Vector3Int.zero), $"lower q={q}");
                var crossing = Vertical(1.99999f, 2-2*q, 3*q*q+2-4*q, 9*q*q-6*q+2);
                Assert.IsTrue(crossing.Intersects(Vector3Int.zero, Vector3Int.zero), $"crossing q={q}");
            }
        }

        [Test]
        public void CubicNonBinaryUpperTangenciesStayOutside()
        {
            for (var q = 1; q <= 30; q++)
            {
                // y=1+(3qt-1)^2(3t+3) は上面以外へ入らない
                // y=1+(3qt-1)^2(3t+3) never enters through the upper face
                var curve = Vertical(4, 5-6*q, 6-18*q+9*q*q, 54*q*q-36*q+7);
                Assert.IsFalse(curve.Intersects(Vector3Int.zero, Vector3Int.zero), $"cubic q={q}");
            }
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void RoundedStationaryContactRespectsHalfOpenFaces(int axis, bool upper)
        {
            // 下限接触の既知の丸め例を、三軸と上限の対称形でも検証する
            // Check the known rounded lower contact on all axes and its upper-face reflection
            var points = new[] { -2.296875f, -2.40625f, 2.484375f, -1.6875f };
            var controls = new Vector3[4];
            for (var i = 0; i < controls.Length; i++)
            {
                controls[i] = Vector3.one * .5f;
                controls[i][axis] = upper ? 1 - points[i] : points[i];
            }
            var curve = new BezierCellBoxIntersection(controls[0], controls[1], controls[2], controls[3]);
            Assert.AreEqual(!upper, curve.Intersects(Vector3Int.zero, Vector3Int.zero));
        }

        private static BezierCellBoxIntersection Vertical(float p0, float p1, float p2, float p3)
        {
            return new BezierCellBoxIntersection(new Vector3(.5f, p0, .5f), new Vector3(.5f, p1, .5f), new Vector3(.5f, p2, .5f), new Vector3(.5f, p3, .5f));
        }
    }
}

using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class TrainRailCurvePlacementRuleTest
    {
        [Test]
        public void IsPlaceable_AllowsStraightRail()
        {
            // 直線は曲率0なのでR無限大として許可する。
            // A straight segment has zero curvature, so it is allowed as infinite radius.
            var p0 = new Vector3(0f, 0f, 0f);
            var p1 = new Vector3(4f, 0f, 0f);
            var p2 = new Vector3(8f, 0f, 0f);
            var p3 = new Vector3(12f, 0f, 0f);

            var minimumRadius = TrainRailCurvePlacementRule.CalculateMinimumCurveRadius(p0, p1, p2, p3);

            Assert.That(minimumRadius, Is.GreaterThanOrEqualTo(TrainRailCurvePlacementRule.MinimumPlaceableCurveRadius));
            Assert.IsTrue(TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3));
        }

        [Test]
        public void IsPlaceable_BlocksTightCurveUnderMinimumRadius()
        {
            // 近距離で直交方向へ曲げると最小Rがしきい値未満になる。
            // A short orthogonal turn falls below the minimum sampled radius.
            var p0 = new Vector3(0f, 0f, 0f);
            var p1 = new Vector3(2f, 0f, 0f);
            var p2 = new Vector3(0f, 0f, 2f);
            var p3 = new Vector3(2f, 0f, 2f);

            Assert.IsFalse(TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3));
        }

        [Test]
        public void IsPlaceable_AllowsWideCurveOverMinimumRadius()
        {
            // 同じ曲がりでも十分な距離があれば最小Rが大きくなる。
            // The same turn shape becomes placeable when the radius grows with distance.
            var p0 = new Vector3(0f, 0f, 0f);
            var p1 = new Vector3(40f, 0f, 0f);
            var p2 = new Vector3(0f, 0f, 40f);
            var p3 = new Vector3(40f, 0f, 40f);

            var minimumRadius = TrainRailCurvePlacementRule.CalculateMinimumCurveRadius(p0, p1, p2, p3);

            Assert.That(minimumRadius, Is.GreaterThanOrEqualTo(TrainRailCurvePlacementRule.MinimumPlaceableCurveRadius));
            Assert.IsTrue(TrainRailCurvePlacementRule.IsPlaceable(p0, p1, p2, p3));
        }
    }
}

using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using NUnit.Framework;

namespace Client.Tests
{
    public class ConveyorVerticalEnvelopeTest
    {
        [Test]
        public void FlatNoObstacle_ReturnsIdentity()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 0, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true }, feasible);
        }

        [Test]
        public void SingleHeight1Obstacle_RaisesAndReturns()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 1, 0, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 0, 1, 0, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true, true }, feasible);
        }

        [Test]
        public void Height2Obstacle_BuildsTwoCellRamps()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 0, 2, 0, 0, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 0, 1, 2, 1, 0, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true, true, true, true }, feasible);
        }

        [Test]
        public void ObstacleTooCloseToEnds_MarksEndpointsInfeasible()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 2, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 1, 2, 1 }, beltY);
            Assert.AreEqual(new[] { false, true, false }, feasible);
        }

        [Test]
        public void ExistingUpRampBaseline_IsIdempotent()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 1, 1, 2 }, 1, 2, -1);
            Assert.AreEqual(new[] { 1, 1, 2 }, beltY);
            Assert.AreEqual(new[] { true, true, true }, feasible);
        }

        [Test]
        public void ObstacleAtCorner_FlattensIntoPlateau()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 1, 0, 0 }, 0, 0, 2);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 0 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true, true }, feasible);
        }

        // 不具合1: 1セル間隔の2障害物。谷を作らず橋渡しする
        // Bug 1: two obstacles one cell apart must bridge (no valley) instead of dipping.
        [Test]
        public void TwoObstaclesGap1_BridgesFlat()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 0, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 0 }, beltY);
        }

        // 不具合2: 2セル間隔の2障害物。V字にせず橋渡しする
        // Bug 2: two obstacles two cells apart must bridge (no V) instead of dipping.
        [Test]
        public void TwoObstaclesGap2_BridgesFlat()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 0, 0, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 1, 0 }, beltY);
        }

        // 3セル間隔は地面まで降りて平坦区間を作れるので谷を残す
        // A three-cell gap is wide enough to descend with a flat floor, so the valley stays.
        [Test]
        public void TwoObstaclesGap3_DescendsToGround()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 0, 0, 0, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 0, 0, 0, 1, 0 }, beltY);
        }

        // 近接3障害物は1本の橋にまとめる
        // Three close obstacles merge into a single bridge.
        [Test]
        public void ThreeObstaclesClose_MergeIntoOneBridge()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 0, 1, 0, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 1, 1, 0 }, beltY);
        }

        // 近接ペアは橋渡し、離れた障害物との間は地面に降りる（混在）
        // A close pair bridges while the far obstacle is reached via a ground descent (mixed).
        [Test]
        public void CloseAndWideMix_BridgesOnlyTheNarrowGap()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 0, 1, 0, 0, 0, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 0, 0, 0, 1, 0 }, beltY);
        }

        // 高さ2障害物間ギャップ4は橋渡し（地面復帰に幅5要る）
        // Height-2 obstacles with a 4-cell gap bridge (descending to ground needs width 5).
        [Test]
        public void Height2Gap4_BridgesAtRimHeight()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 2, 0, 0, 0, 0, 2, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 2, 2, 2, 2, 2, 2, 1, 0 }, beltY);
        }

        // 高さ2障害物間ギャップ5は地面まで降りる
        // Height-2 obstacles with a 5-cell gap descend to ground.
        [Test]
        public void Height2Gap5_DescendsToGround()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 2, 0, 0, 0, 0, 0, 2, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 2, 1, 0, 0, 0, 1, 2, 1, 0 }, beltY);
        }

        // 左右非対称(高2と高1)で狭い間は低い肩の高さで橋渡しする
        // Asymmetric rims (height 2 and 1) bridge at the lower rim when the gap is narrow.
        [Test]
        public void AsymmetricRimsNarrow_BridgesAtLowerRim()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 2, 0, 0, 1, 0 }, 0, 0, -1);
            Assert.AreEqual(new[] { 0, 1, 2, 1, 1, 1, 0 }, beltY);
        }

        // コーナー付近の近接2障害物も踊り場+橋渡しで平坦化する
        // Two close obstacles near the corner flatten via plateau + bridge.
        [Test]
        public void TwoObstaclesGap1AtCorner_FlattensIntoPlateau()
        {
            var (beltY, _) = ConveyorVerticalEnvelope.Solve(new[] { 0, 1, 0, 1, 0 }, 0, 0, 2);
            Assert.AreEqual(new[] { 0, 1, 1, 1, 0 }, beltY);
        }

        // 上り坂ドラッグ(端点高さが異なる)は単調ランプを保つ
        // An up-slope drag (differing endpoint heights) keeps a monotonic ramp.
        [Test]
        public void UpSlopeDrag_KeepsMonotonicRamp()
        {
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 0, 0, 0, 1 }, 0, 1, -1);
            Assert.AreEqual(new[] { 0, 0, 0, 1 }, beltY);
            Assert.AreEqual(new[] { true, true, true, true }, feasible);
        }

        // 始点直上が障害物だと端点を上げられず設置不可
        // An obstacle on the start cell cannot raise the fixed endpoint -> infeasible.
        [Test]
        public void ObstacleOnStartCell_MarksStartInfeasible()
        {
            var (_, feasible) = ConveyorVerticalEnvelope.Solve(new[] { 1, 0, 0 }, 0, 0, -1);
            Assert.IsFalse(feasible[0]);
        }
    }
}

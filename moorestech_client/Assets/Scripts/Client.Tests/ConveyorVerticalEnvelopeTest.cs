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
    }
}

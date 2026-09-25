using Core.Master;
using Core.Update;
using Game.Train.Unit.Motion;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.TrainMotion
{
    public class TrainEffectiveWeightCalculatorTest
    {
        private const int ReferenceWeight = 120000;

        [Test]
        public void ExponentOne_KeepsRealWeight()
        {
            Assert.AreEqual(160000, TrainEffectiveWeightCalculator.Calculate(160000, 1, ReferenceWeight), 1e-6);
            Assert.AreEqual(40000, TrainEffectiveWeightCalculator.Calculate(40000, 1, ReferenceWeight), 1e-6);
        }

        [Test]
        public void ExponentZero_IgnoresWeight()
        {
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(40000, 0, ReferenceWeight), 1e-6);
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(480000, 0, ReferenceWeight), 1e-6);
        }

        [Test]
        public void ExponentHalf_ShrinksHeavyTrainAndKeepsReferenceWeight()
        {
            // 基準4倍→影響度0.5で実効2倍
            // 4x reference weight -> 2x effective at exponent 0.5
            Assert.AreEqual(ReferenceWeight * 2, TrainEffectiveWeightCalculator.Calculate(ReferenceWeight * 4, 0.5, ReferenceWeight), 1e-6);
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(ReferenceWeight, 0.5, ReferenceWeight), 1e-6);
        }

        [Test]
        public void StepWithTestMaster_MatchesPlainPhysicsAtExponentOne()
        {
            // 影響度1: 加速=牽引力÷重量
            // Exponent 1: acceleration = traction / weight
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            const int totalWeight = 160000;
            const double totalTraction = 400000;
            var input = new TrainMotionStepInput(0, 0, MasterHolder.TrainUnitMaster.MasconLevelMaximum, totalTraction, totalWeight);
            var result = TrainDistanceSimulator.Step(input);

            var expectedAcceleration = totalTraction / totalWeight;
            var afterTraction = expectedAcceleration * GameUpdater.SecondsPerTick;
            // 期待値はマスタ値から独立に計算
            // Expected value is computed independently from master values
            var expectedResistance = MasterHolder.TrainUnitMaster.Friction * 9.80665
                + MasterHolder.TrainUnitMaster.AirResistance * afterTraction * afterTraction / totalWeight;
            var expectedSpeed = afterTraction - expectedResistance * GameUpdater.SecondsPerTick;
            Assert.AreEqual(expectedSpeed, result.NewSpeed, 1e-9);
        }
    }
}

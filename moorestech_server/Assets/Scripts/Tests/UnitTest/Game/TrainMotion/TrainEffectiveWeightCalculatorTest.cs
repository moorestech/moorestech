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
            // 基準の4倍重い編成は影響度0.5で実効2倍になる
            // A train four times the reference weight becomes twice as heavy at exponent 0.5
            Assert.AreEqual(ReferenceWeight * 2, TrainEffectiveWeightCalculator.Calculate(ReferenceWeight * 4, 0.5, ReferenceWeight), 1e-6);
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(ReferenceWeight, 0.5, ReferenceWeight), 1e-6);
        }

        [Test]
        public void StepWithTestMaster_MatchesPlainPhysicsAtExponentOne()
        {
            // テストマスタは影響度1なので、牽引加速は従来の牽引力÷編成重量と一致する
            // The test master uses exponent 1, so traction acceleration equals the old traction / weight
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            const int totalWeight = 160000;
            const double totalTraction = 400000;
            var input = new TrainMotionStepInput(0, 0, MasterHolder.TrainUnitMaster.MasconLevelMaximum, totalTraction, totalWeight);
            var result = TrainDistanceSimulator.Step(input);

            var expectedAcceleration = totalTraction / totalWeight;
            var afterTraction = expectedAcceleration * GameUpdater.SecondsPerTick;
            var expectedSpeed = afterTraction - TrainDistanceSimulator.CalculateResistanceAcceleration(afterTraction, totalWeight) * GameUpdater.SecondsPerTick;
            Assert.AreEqual(expectedSpeed, result.NewSpeed, 1e-9);
        }
    }
}

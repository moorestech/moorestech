using System;
using Core.Master;
using Game.Train.Unit.Motion;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.TrainMotion
{
    public class TrainEffectiveWeightCalculatorTest
    {
        // テストmodの基準重量
        // Reference weight of the test mod
        private const int ReferenceWeight = 120000;

        [SetUp]
        public void LoadTestMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(ReferenceWeight, MasterHolder.TrainUnitMaster.ReferenceWeight);
        }

        [Test]
        public void Calculate_ExponentOneKeepsRealWeightExactly()
        {
            Assert.AreEqual(160100d, TrainEffectiveWeightCalculator.Calculate(160100, 1, ReferenceWeight));
            Assert.AreEqual(40000d, TrainEffectiveWeightCalculator.Calculate(40000, 1, ReferenceWeight));
        }

        [Test]
        public void Calculate_ExponentZeroIgnoresWeight()
        {
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(40000, 0, ReferenceWeight), 1e-6);
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(480000, 0, ReferenceWeight), 1e-6);
        }

        [Test]
        public void Calculate_ExponentHalfShrinksHeavyTrainAndKeepsReferenceWeight()
        {
            // 基準4倍→影響度0.5で実効2倍
            // 4x reference weight -> 2x effective at exponent 0.5
            Assert.AreEqual(ReferenceWeight * 2, TrainEffectiveWeightCalculator.Calculate(ReferenceWeight * 4, 0.5, ReferenceWeight), 1e-6);
            Assert.AreEqual(ReferenceWeight, TrainEffectiveWeightCalculator.Calculate(ReferenceWeight, 0.5, ReferenceWeight), 1e-6);
        }

        [Test]
        public void EffectiveWeight_UsesLocomotiveExponentAndIgnoresWagonExponent()
        {
            // 貨車の影響度は牽引力0なので結果に効かない
            // Wagon exponent has no effect because its traction is zero
            var withWagonExponentZero = Accumulate((240000, 1000, 0.5f), (240000, 0, 0f));
            var withWagonExponentOne = Accumulate((240000, 1000, 0.5f), (240000, 0, 1f));

            var expected = TrainEffectiveWeightCalculator.Calculate(480000, 0.5, ReferenceWeight);
            Assert.AreEqual(expected, withWagonExponentZero, 1e-6);
            Assert.AreEqual(expected, withWagonExponentOne, 1e-6);
        }

        [Test]
        public void EffectiveWeight_MixedLocomotivesUseTractionWeightedExponent()
        {
            // 牽引力比1:3 → 影響度0.875
            // 0.5 at traction 100 and 1.0 at traction 300 -> (50+300)/400 = 0.875
            var actual = Accumulate((200000, 100, 0.5f), (200000, 300, 1f));

            var expected = TrainEffectiveWeightCalculator.Calculate(400000, 0.875, ReferenceWeight);
            Assert.AreEqual(expected, actual, 1e-6);
        }

        [Test]
        public void EffectiveWeight_ZeroTractionTrainUsesRealWeight()
        {
            var actual = Accumulate((200000, 0, 0f), (100000, 0, 0.5f));

            Assert.AreEqual(300000d, actual);
        }

        private static double Accumulate(params (int weight, int traction, float exponent)[] cars)
        {
            var calculator = new TrainEffectiveWeightCalculator();
            foreach (var (weight, traction, exponent) in cars)
            {
                calculator.AddCar(weight, CreateCarMaster(weight, traction, exponent));
            }
            return calculator.CalculateEffectiveWeight();
        }

        private static TrainCarMasterElement CreateCarMaster(int weight, int traction, float exponent)
        {
            return new TrainCarMasterElement(0, Guid.NewGuid(), null, false, null, weight, traction, exponent, 1, 1, "None", 0f, null, null, 0, "TestCar");
        }
    }
}

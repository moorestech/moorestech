using Game.Block.Blocks.Gear;
using Game.EnergySystem;
using Game.Gear.Common;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class GearConsumptionFormulaTest
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void 定格RPMでは必要トルクがbaseTorqueに一致する()
        {
            var torque = GearConsumptionCalculator.CalcRequiredTorque(
                baseRpm: 100f, minimumRpm: 100f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(100f));
            Assert.AreEqual(1f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 半速では必要トルクが2乗則で0_25倍になる()
        {
            var torque = GearConsumptionCalculator.CalcRequiredTorque(
                baseRpm: 100f, minimumRpm: 0f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(50f));
            Assert.AreEqual(0.25f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 倍速では必要トルクが1_585乗則で約3倍になる()
        {
            var torque = GearConsumptionCalculator.CalcRequiredTorque(
                baseRpm: 100f, minimumRpm: 0f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(200f));
            Assert.AreEqual(3.0f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 下限未満のRPMでは必要トルクが0になる()
        {
            var torque = GearConsumptionCalculator.CalcRequiredTorque(
                baseRpm: 100f, minimumRpm: 20f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(10f));
            Assert.AreEqual(0f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 下限ぴったりでは2乗則で必要トルクが算出される()
        {
            var torque = GearConsumptionCalculator.CalcRequiredTorque(
                baseRpm: 100f, minimumRpm: 20f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(20f));
            Assert.AreEqual(0.04f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 倍速で供給トルクが要求通りなら稼働率は2_0()
        {
            var rate = GearConsumptionCalculator.CalcOperatingRate(
                baseRpm: 100f, minimumRpm: 0f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(200f), currentTorque: new Torque(3.0f));
            Assert.AreEqual(2.0f, rate, Tolerance);
        }

        [Test]
        public void 倍速でトルク供給が半分なら稼働率は1_0()
        {
            var rate = GearConsumptionCalculator.CalcOperatingRate(
                baseRpm: 100f, minimumRpm: 0f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(200f), currentTorque: new Torque(1.5f));
            Assert.AreEqual(1.0f, rate, Tolerance);
        }

        [Test]
        public void 下限未満RPMでは稼働率が0()
        {
            var rate = GearConsumptionCalculator.CalcOperatingRate(
                baseRpm: 100f, minimumRpm: 50f, baseTorque: 1f, expUnder: 2f, expOver: 1.585f,
                currentRpm: new RPM(10f), currentTorque: new Torque(10f));
            Assert.AreEqual(0f, rate, Tolerance);
        }

        [Test]
        public void 指数を変えると消費カーブが変わる()
        {
            // b=3, currentRpm=baseRpm/2 → 0.5^3 = 0.125
            var torque = GearConsumptionCalculator.CalcRequiredTorque(
                baseRpm: 100f, minimumRpm: 0f, baseTorque: 1f, expUnder: 3f, expOver: 1.585f,
                currentRpm: new RPM(50f));
            Assert.AreEqual(0.125f, torque.AsPrimitive(), Tolerance);
        }
    }
}

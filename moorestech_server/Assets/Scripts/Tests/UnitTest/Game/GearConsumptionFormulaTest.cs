using Game.Block.Blocks.Gear;
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
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 100, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);
            var torque = GearConsumptionCalculator.CalcRequiredTorque(consumption, new RPM(100f));
            Assert.AreEqual(1f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 半速では必要トルクが2乗則で0_25倍になる()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);
            var torque = GearConsumptionCalculator.CalcRequiredTorque(consumption, new RPM(50f));
            Assert.AreEqual(0.25f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 倍速では必要トルクが1_585乗則で約3倍になる()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);
            var torque = GearConsumptionCalculator.CalcRequiredTorque(consumption, new RPM(200f));
            Assert.AreEqual(3.0f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 下限未満のRPMでは必要トルクが0になる()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 20, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);
            var torque = GearConsumptionCalculator.CalcRequiredTorque(consumption, new RPM(10f));
            Assert.AreEqual(0f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 下限ぴったりでは2乗則で必要トルクが算出される()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 20, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);
            var torque = GearConsumptionCalculator.CalcRequiredTorque(consumption, new RPM(20f));
            Assert.AreEqual(0.04f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 指数を変えると消費カーブが変わる()
        {
            // b=3, currentRpm=baseRpm/2 → 0.5^3 = 0.125
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 3, torqueExponentOver: 1.585);
            var torque = GearConsumptionCalculator.CalcRequiredTorque(consumption, new RPM(50f));
            Assert.AreEqual(0.125f, torque.AsPrimitive(), Tolerance);
        }

        [Test]
        public void 要求率1では供給が定格を超えず稼働率は1で頭打ちになる()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);

            // 定格トルクの2倍を与えても、要求率1なら稼働率は1で頭打ちになる
            // Twice the rated torque still caps the operating rate at 1 when the request rate is 1
            var operatingRate = GearConsumptionCalculator.CalcOperatingRate(consumption, new RPM(100f), new Torque(2f), 1f);
            Assert.AreEqual(1f, operatingRate, Tolerance);
        }

        [Test]
        public void 要求率が1を超えると供給側の上限も同じ倍率まで開く()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);

            // 要求率1.5に見合うトルク(必要トルク×1.5)が届けば稼働率も1.5まで伸びる
            // Delivering torque matching a 1.5 request rate (required × 1.5) lets the operating rate reach 1.5
            var satisfied = GearConsumptionCalculator.CalcOperatingRate(consumption, new RPM(100f), new Torque(1.5f), 1.5f);
            Assert.AreEqual(1.5f, satisfied, Tolerance);

            // トルクが定格どまりなら、要求率を上げても供給は増えない
            // With only the rated torque available, raising the request rate does not increase supply
            var starved = GearConsumptionCalculator.CalcOperatingRate(consumption, new RPM(100f), new Torque(1f), 1.5f);
            Assert.AreEqual(1f, starved, Tolerance);
        }

        [Test]
        public void 要求率0では稼働率も0になる()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);
            var operatingRate = GearConsumptionCalculator.CalcOperatingRate(consumption, new RPM(100f), new Torque(1f), 0f);
            Assert.AreEqual(0f, operatingRate, Tolerance);
        }

        [Test]
        public void 供給電力は要求率込みの稼働率で基準電力を上回る()
        {
            var consumption = GearConsumptionTestFactory.Create(
                baseRpm: 100, minimumRpm: 0, baseTorque: 1, torqueExponentUnder: 2, torqueExponentOver: 1.585);

            // 要求率1.5が満たされたときの供給電力は基準電力(baseTorque×baseRpm)の1.5倍
            // When a 1.5 request rate is satisfied, supplied power is 1.5x the base power (baseTorque x baseRpm)
            var operatingRate = GearConsumptionCalculator.CalcOperatingRate(consumption, new RPM(100f), new Torque(1.5f), 1.5f);
            var power = GearConsumptionCalculator.CalcCurrentPower(consumption, operatingRate);
            Assert.AreEqual(150f, power.AsPrimitive(), Tolerance);
        }
    }
}
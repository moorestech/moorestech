using Core.Master.Validator;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class CleanRoomYieldTableRulesTest
    {
        [Test]
        public void ContractClassRowsAreValidTest()
        {
            var rows = new (string, float, float, float)[] { ("A", 1.0f, 0.10f, 0.15f), ("B", 0.75f, 0.15f, 0.22f), ("C", 0.5f, 0.20f, 0.30f), ("D", 0.25f, 0.25f, 0.38f) };
            Assert.AreEqual("", CleanRoomYieldTableRules.ValidateClassRows(rows));
        }

        [Test]
        public void WorseClassWithHigherMultiplierIsRejectedTest()
        {
            // 下のクラスが品質倍率で上回ると、クラスを上げて歩留まりが下がりうる
            // A worse class with a higher multiplier could let a better class yield less
            var rows = new (string, float, float, float)[] { ("A", 0.5f, 0.10f, 0.15f), ("B", 0.75f, 0.15f, 0.22f) };
            StringAssert.Contains("QualityMultiplier", CleanRoomYieldTableRules.ValidateClassRows(rows));
        }

        [Test]
        public void WorseClassWithLowerFailRateIsRejectedTest()
        {
            var layerRows = new (string, float, float, float)[] { ("A", 1.0f, 0.20f, 0.15f), ("B", 0.75f, 0.15f, 0.22f) };
            StringAssert.Contains("LayerFailRate", CleanRoomYieldTableRules.ValidateClassRows(layerRows));
            var dieRows = new (string, float, float, float)[] { ("A", 1.0f, 0.10f, 0.30f), ("B", 0.75f, 0.15f, 0.22f) };
            StringAssert.Contains("DieLossRate", CleanRoomYieldTableRules.ValidateClassRows(dieRows));
        }

        [TestCase(-0.1f, 0.1f, 0.1f, "QualityMultiplier")]
        [TestCase(1.0f, 1.1f, 0.1f, "LayerFailRate")]
        [TestCase(1.0f, 0.1f, -0.1f, "DieLossRate")]
        public void OutOfRangeValueIsRejectedTest(float qualityMultiplier, float layerFailRate, float dieLossRate, string expectedKey)
        {
            var rows = new (string, float, float, float)[] { ("A", qualityMultiplier, layerFailRate, dieLossRate) };
            StringAssert.Contains(expectedKey, CleanRoomYieldTableRules.ValidateClassRows(rows));
        }
    }
}

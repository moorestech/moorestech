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

        [Test]
        public void ContractDieTablesAreValidTest()
        {
            var tables = new[]
            {
                new[] { 1f },
                new[] { 0.7f, 0.3f },
                new[] { 0.55f, 0.3f, 0.15f },
                new[] { 0.45f, 0.3f, 0.17f, 0.08f },
                new[] { 0.4f, 0.28f, 0.18f, 0.09f, 0.05f },
            };
            Assert.AreEqual("", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 5));
        }

        [Test]
        public void TableAboveItsGradeIsRejectedTest()
        {
            // G1の表がLv2を持つと、品質モジュール無しでもLv2が出てしまう（品質ゲート破り）
            // A G1 table holding Lv2 would leak Lv2 without any quality module (breaking the quality gate)
            var tables = new[] { new[] { 0.5f, 0.5f } };
            StringAssert.Contains("GradeDieTables[0]", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void TableNotSummingToOneIsRejectedTest()
        {
            var tables = new[] { new[] { 1f }, new[] { 0.7f, 0.2f } };
            StringAssert.Contains("sum", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void NegativeRateIsRejectedTest()
        {
            var tables = new[] { new[] { 1f }, new[] { 1.1f, -0.1f } };
            StringAssert.Contains("negative", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void ShrinkingUpperTailIsRejectedTest()
        {
            // G3のLv2以上(0.2)がG2(0.3)を下回ると、グレードが上がってLv2以上が減りうる
            // G3's Lv2+ share (0.2) below G2's (0.3) would let a higher grade lower the Lv2+ count
            var tables = new[] { new[] { 1f }, new[] { 0.7f, 0.3f }, new[] { 0.8f, 0.1f, 0.1f } };
            StringAssert.Contains("tail", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }
    }
}

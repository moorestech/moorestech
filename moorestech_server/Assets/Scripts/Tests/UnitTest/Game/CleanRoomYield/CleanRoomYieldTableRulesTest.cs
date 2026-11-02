using System.Collections.Generic;
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
        public void NonFiniteClassValuesAreRejectedTest([Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity)] float value)
        {
            // 各入力を独立に壊し、他の値で拒否されていないことを確かめる
            // Corrupt each input independently so another invalid field cannot mask acceptance
            StringAssert.Contains("QualityMultiplier", CleanRoomYieldTableRules.ValidateClassRows(new[] { ("A", value, 0f, 0f) }));
            StringAssert.Contains("LayerFailRate", CleanRoomYieldTableRules.ValidateClassRows(new[] { ("A", 1f, value, 0f) }));
            StringAssert.Contains("DieLossRate", CleanRoomYieldTableRules.ValidateClassRows(new[] { ("A", 1f, 0f, value) }));
        }

        [Test]
        public void FiniteBoundaryValuesAreAcceptedTest([Values(0f, 1f)] float value)
        {
            Assert.AreEqual("", CleanRoomYieldTableRules.ValidateClassRows(new[] { ("A", value, value, value) }));

        }

        [Test]
        public void NonFiniteDieRateIsRejectedTest([Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity)] float value)
        {
            var tables = Tables();
            tables[1] = (new[] { value, 0.1f, 0f }, 0.2f);
            StringAssert.Contains("non-finite", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
            tables = Tables();
            tables[1] = (tables[1].Item1, value);
            StringAssert.Contains("FailedRate", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void ContractDieTablesAreValidTest()
        {
            var tables = new (IReadOnlyList<float>, float)[]
            {
                (new[] { .20f, 0f, 0f, 0f, 0f }, .80f),
                (new[] { .70f, .10f, 0f, 0f, 0f }, .20f),
                (new[] { .10f, .70f, .10f, 0f, 0f }, .10f),
                (new[] { .04f, .08f, .70f, .10f, 0f }, .08f),
                (new[] { .02f, .04f, .06f, .70f, .10f }, .08f),
                (new[] { .02f, .03f, .05f, .10f, .70f }, .10f),
            };
            Assert.AreEqual("", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 5));
        }

        [Test]
        public void TierTableAboveQualityGateIsRejectedTest()
        {
            var tables = Tables();
            tables[1] = (new[] { .7f, 0f, .1f }, .2f);
            StringAssert.Contains("quality gate", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void TableNotSummingToOneIsRejectedTest()
        {
            var tables = Tables();
            tables[1] = (tables[1].Item1, .3f);
            StringAssert.Contains("sum", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void NegativeRateIsRejectedTest()
        {
            var tables = Tables();
            tables[1] = (new[] { .9f, -.1f, 0f }, .2f);
            StringAssert.Contains("negative", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
            tables[1] = (new[] { .9f, .2f, 0f }, -.1f);
            StringAssert.Contains("negative", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void MissingDefaultLevelIsRejectedTest()
        {
            var tables = Tables();
            tables[2] = (new[] { .8f, 0f, .1f }, .1f);
            StringAssert.Contains("default Lv2", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        [Test]
        public void WrongTableCountIsRejectedTest()
        {
            StringAssert.Contains("requires 5 quality tier tables", CleanRoomYieldTableRules.ValidateDieTables("Dicings", Tables(), 4));
        }

        [Test]
        public void WrongLevelCountIsRejectedTest()
        {
            var tables = Tables();
            tables[0] = (new[] { .2f }, .8f);
            StringAssert.Contains("requires 3 levels", CleanRoomYieldTableRules.ValidateDieTables("Dicings", tables, 3));
        }

        private static (IReadOnlyList<float>, float)[] Tables()
        {
            return new (IReadOnlyList<float>, float)[]
            {
                (new[] { .2f, 0f, 0f }, .8f),
                (new[] { .7f, .1f, 0f }, .2f),
                (new[] { .1f, .7f, .1f }, .1f),
                (new[] { .05f, .1f, .75f }, .1f),
            };
        }
    }
}

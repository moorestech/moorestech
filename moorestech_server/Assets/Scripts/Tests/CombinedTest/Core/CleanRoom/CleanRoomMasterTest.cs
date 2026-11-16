using System;
using System.IO;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomMasterTest
    {
        [Test]
        public void ThresholdsLoadedInOrderTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.IsAvailable);
            Assert.AreEqual(4, master.Thresholds.Count);
            // 良い順で必要換気回数が単調減少
            // Rows are ordered best-to-worst with monotonically decreasing required air change rates
            Assert.AreEqual(0.017, master.Thresholds[0].RequiredAirChangeRate, 0.0001);
            Assert.AreEqual(0.0014, master.Thresholds[3].RequiredAirChangeRate, 0.0001);
            Assert.AreEqual(4, master.OutThresholdIndex);
        }

        [Test]
        public void EmptyMasterFallbackTest()
        {
            var empty = CleanRoomMaster.CreateEmpty();
            Assert.IsFalse(empty.IsAvailable);
            Assert.AreEqual(0, empty.Thresholds.Count);
        }

        [Test]
        public void TryGetThresholdIndexByClassNameTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomMaster;
            // 既存クラス名は行順に依存せず自身のインデックスへ解決される
            // An existing class name resolves to its own index independently of row order
            Assert.IsTrue(master.TryGetThresholdIndexByClassName("A", out var indexA));
            Assert.AreEqual(0, indexA);
            Assert.IsTrue(master.TryGetThresholdIndexByClassName("C", out var indexC));
            Assert.AreEqual(2, indexC);
            Assert.IsFalse(master.TryGetThresholdIndexByClassName("Z", out _));
        }

        [Test]
        public void ThresholdsCarryClassYieldValuesTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // クラス行ごとに品質倍率・層失敗率・ダイ損失率が読める
            // Each class row exposes its quality multiplier, layer fail rate and die loss rate
            var thresholds = MasterHolder.CleanRoomMaster.Thresholds;
            Assert.AreEqual(1.0f, thresholds[0].QualityMultiplier, 1e-6f);
            Assert.AreEqual(0.10f, thresholds[0].LayerFailRate, 1e-6f);
            Assert.AreEqual(0.15f, thresholds[0].DieLossRate, 1e-6f);
            Assert.AreEqual(0.25f, thresholds[3].QualityMultiplier, 1e-6f);
            Assert.AreEqual(0.25f, thresholds[3].LayerFailRate, 1e-6f);
            Assert.AreEqual(0.38f, thresholds[3].DieLossRate, 1e-6f);
        }

        [Test]
        public void DepositionRecipeHasNoRoleTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 成膜はグレード抽選を失い、役割を持たない普通の工程になる
            // Deposition lost its grade draw and is now a plain process with no role
            Assert.IsFalse(MasterHolder.CleanRoomMaster.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, out _));
        }

        [Test]
        public void TryGetRecipeRoleResolvesLayerExposureTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 層露光レシピはEUVの役割として引け、次層ウェハと失敗品の出力の添字が索引時に解決済み
            // The layer-exposure recipe resolves as an EUV role with the layered and failed output indices already resolved at indexing
            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, out var role));
            Assert.IsInstanceOf<LayerExposureRecipeRole>(role);
            var layerExposure = (LayerExposureRecipeRole)role;
            Assert.AreEqual(Guid.Parse("1bbb9ba9-403f-47ab-a601-ae74fe444acd"), layerExposure.Exposure.LayeredWaferItemGuid);
            Assert.AreEqual(Guid.Parse("4f0fa5e5-121e-4c92-a0cb-a9ef810921a8"), layerExposure.Exposure.FailedWaferItemGuid);
            Assert.AreEqual(0, layerExposure.LayeredOutputIndex);
            Assert.AreEqual(1, layerExposure.FailedOutputIndex);

            // 成膜レシピはEUVの役割にならない（1レシピ1役割）
            // The deposition recipe does not resolve as an EUV role (one role per recipe)
            Assert.IsFalse(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, out var depositionRole));
            Assert.IsNotInstanceOf<LayerExposureRecipeRole>(depositionRole);
        }

        [Test]
        public void TryGetRecipeRoleResolvesDicingTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ダイシングの表は品質Tier 0〜最大レベルの順に並び、1枚あたりのダイ数とダイのレベル数が索引時に解決済み
            // The dicing tables are ordered by quality tier 0..max level, and the dies per wafer and die level count are resolved at indexing
            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, out var role));
            Assert.IsInstanceOf<DicingRecipeRole>(role);
            var dicing = (DicingRecipeRole)role;
            Assert.AreEqual(300, dicing.Dicing.DiesPerWafer);
            Assert.AreEqual(4, dicing.Dicing.QualityTierDieTables.Length);
            Assert.AreEqual(3, dicing.Dicing.QualityTierDieTables[2].LevelRates.Length);
            Assert.AreEqual(3, dicing.DieLevelCount);

            // 層露光レシピはダイシングの役割にならない（1レシピ1役割）
            // The layer-exposure recipe does not resolve as a dicing role (one role per recipe)
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, out var exposureRole));
            Assert.IsNotInstanceOf<DicingRecipeRole>(exposureRole);
        }

        [Test]
        public void DicingRoleRejectsInheritanceRecipeTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ダイシングの役割表が継承レシピを指すと、起動時の検証が levelInheritance None を求めて止める
            // When the dicing role table points at an inheritance recipe, boot validation stops it by requiring levelInheritance None
            var json = JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));
            json["data"][0]["dicings"][0]["machineRecipeGuid"] = ForUnitTestLevelInheritanceIds.InheritRecipeGuid.ToString();
            Assert.IsFalse(new CleanRoomMaster(json).Validate(out var logs));
            StringAssert.Contains($"Dicings MachineRecipeGuid:{ForUnitTestLevelInheritanceIds.InheritRecipeGuid} levelInheritance must be None", logs);
        }

        [Test]
        public void RoleChanceRequiresFiniteUnitIntervalTest(
            [Values("layerExposures", "dicings")] string role,
            [Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, 1f)] float value)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var json = JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));
            var key = role == "layerExposures" ? "maxFailReduction" : "maxRareShift";
            // JSONモデル境界で非有限値と正常な両端を検証する
            // Validate non-finite values and both valid endpoints at the JSON model boundary
            json["data"][0][role][0][key] = value;
            var valid = new CleanRoomMaster(json).Validate(out var logs);
            Assert.AreEqual(!float.IsNaN(value) && !float.IsInfinity(value), valid, logs);
            if (!valid) StringAssert.Contains(char.ToUpperInvariant(key[0]) + key.Substring(1), logs);
        }

        [Test]
        public void DicingCapacityMultiplicationCannotOverflowTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var json = JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));
            json["data"][0]["dicings"][0]["diesPerWafer"] = 1073741824;
            Assert.IsFalse(new CleanRoomMaster(json).Validate(out var logs));
            StringAssert.Contains("2147483648", logs);
        }

        [Test]
        public void TestMasterPassesValidationTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(new CleanRoomMaster(LoadTestCleanRoomJson()).Validate(out var logs), logs);
        }

        [Test]
        public void RequiredAirChangeRatesMustDescendTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 行Bの必要換気回数を行A以上にすると、良い行から走査するクラス判定が成り立たないため起動時に止まる
            // Raising row B's required rate to row A's or above breaks the best-first class scan, so boot validation stops it
            var json = LoadTestCleanRoomJson();
            json["data"][0]["thresholds"][1]["requiredAirChangeRate"] = 0.017;
            Assert.IsFalse(new CleanRoomMaster(json).Validate(out var logs));
            StringAssert.Contains("Thresholds[1] ClassName:B RequiredAirChangeRate:0.017 is not descending", logs);
        }

        #region TestHelper

        private static JObject LoadTestCleanRoomJson()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));
        }

        #endregion
    }
}

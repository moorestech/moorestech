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
            // 良い順（A→D）で並び、濃度上限が単調増加であること
            // Rows are ordered best-to-worst with monotonically increasing concentration caps
            Assert.AreEqual(10.0, master.Thresholds[0].MaxConcentration, 0.0001);
            Assert.AreEqual(1000.0, master.Thresholds[3].MaxConcentration, 0.0001);
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
        public void TryGetRecipeRoleResolvesWaferGradeTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 成膜レシピは成膜の役割として引け、出力の添字・基準アイテム・グレード数が索引時に解決済み
            // The deposition recipe resolves as a deposition role with its output index, base item and grade count already resolved at indexing
            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, out var role));
            Assert.IsInstanceOf<WaferGradeRecipeRole>(role);
            var waferGrade = (WaferGradeRecipeRole)role;
            Assert.AreEqual(Guid.Parse("75a25126-5568-40d8-8028-509f202c4207"), waferGrade.Draw.GradedWaferItemGuid);
            Assert.AreEqual(0.9f, waferGrade.Draw.MaxPromotionChance, 1e-6f);
            Assert.AreEqual(0, waferGrade.GradedOutputIndex);
            Assert.AreEqual(ForUnitTestItemId.TestGradedWaferG1, waferGrade.GradedWaferBaseItemId);
            Assert.AreEqual(3, waferGrade.GradeCount);

            // 役割表に無いGUIDは引けない
            // A GUID outside the role tables does not resolve
            Assert.IsFalse(master.TryGetRecipeRole(Guid.NewGuid(), out _));
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
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, out var depositionRole));
            Assert.IsNotInstanceOf<LayerExposureRecipeRole>(depositionRole);
        }

        [Test]
        public void TryGetRecipeRoleResolvesDicingTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ダイシングの表はグレード順に並び、1枚あたりのダイ数とダイのレベル数が索引時に解決済み
            // The dicing tables are ordered by grade, and the dies per wafer and die level count are resolved at indexing
            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, out var role));
            Assert.IsInstanceOf<DicingRecipeRole>(role);
            var dicing = (DicingRecipeRole)role;
            Assert.AreEqual(300, dicing.Dicing.DiesPerWafer);
            Assert.AreEqual(3, dicing.Dicing.GradeDieTables.Length);
            Assert.AreEqual(3, dicing.Dicing.GradeDieTables[2].LevelRates.Length);
            Assert.AreEqual(3, dicing.DieLevelCount);

            // 層露光レシピはダイシングの役割にならない（1レシピ1役割）
            // The layer-exposure recipe does not resolve as a dicing role (one role per recipe)
            Assert.IsTrue(master.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, out var exposureRole));
            Assert.IsNotInstanceOf<DicingRecipeRole>(exposureRole);
        }

        [Test]
        public void WaferGradeRoleRejectsInheritanceRecipeTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 成膜の役割表が継承レシピ（Part C のテスト用）を指すと、起動時の検証が levelInheritance None を求めて止める
            // When the deposition role table points at an inheritance recipe (Part C's test recipe), boot validation stops it by requiring levelInheritance None
            var cleanRoomJson = JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));
            var draw = (JObject)cleanRoomJson["data"][0]["waferGradeDraws"][0];
            draw["machineRecipeGuid"] = ForUnitTestLevelInheritanceIds.InheritRecipeGuid.ToString();
            draw["gradedWaferItemGuid"] = ForUnitTestLevelInheritanceIds.TargetBaseGuid.ToString();

            Assert.IsFalse(new CleanRoomMaster(cleanRoomJson).Validate(out var errorLogs));
            StringAssert.Contains($"WaferGradeDraws MachineRecipeGuid:{ForUnitTestLevelInheritanceIds.InheritRecipeGuid} must use levelInheritance None", errorLogs);
        }
    }
}

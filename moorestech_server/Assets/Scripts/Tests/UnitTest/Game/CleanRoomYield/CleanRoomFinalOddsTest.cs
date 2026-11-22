using System;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class CleanRoomFinalOddsTest
    {
        [Test]
        public void DicingFinalOddsMultiplySurvivalAndLeaveTheRestAsFailureTest()
        {
            var (dicing, _) = LoadRoles();

            // 損失0.2・良品内訳 [0.5, 0.3, 0]・失敗ダイ0.2 → Lv1 0.8×0.5=0.4、Lv2 0.8×0.3=0.24、失敗 0.2+0.8×0.2=0.36
            // Loss 0.2, survivor split [0.5, 0.3, 0], failed 0.2 -> Lv1 0.4, Lv2 0.24, failure 0.2 + 0.8 x 0.2 = 0.36
            var odds = (DicingFinalOdds)CleanRoomFinalOdds.Of(dicing, new DicingPreview(0.2, 1, new[] { 0.5, 0.3, 0d }, 0.2));
            Assert.AreEqual(((DicingRecipeRole)dicing).Dicing.DiesPerWafer, odds.OutputCount);
            Assert.That(odds.LevelRates, Is.EqualTo(new[] { 0.4, 0.24, 0d }).Within(1e-9));
            Assert.AreEqual(0.36, odds.FailedRate, 1e-9);
        }

        [Test]
        public void LayerExposureFinalOddsIsTheLayerFailRateTest()
        {
            var (_, layer) = LoadRoles();

            // EUV はLv別を持たず、失敗＝層の失敗率
            // EUV has no levels and its failure is the layer fail rate
            var odds = CleanRoomFinalOdds.Of(layer, new LayerExposurePreview(0.308));
            Assert.IsInstanceOf<LayerExposureFinalOdds>(odds);
            Assert.IsEmpty(odds.LevelRates);
            Assert.AreEqual(0.308, odds.FailedRate, 1e-9);
        }

        [Test]
        public void MismatchedRoleAndRatePairsThrowTest()
        {
            var (dicing, layer) = LoadRoles();

            // 役割と率の組の食い違いは呼び出し側の約束違反
            // Mismatched role/rate pairs break the caller's contract
            Assert.Throws<ArgumentOutOfRangeException>(() => CleanRoomFinalOdds.Of(dicing, new LayerExposurePreview(0.308)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CleanRoomFinalOdds.Of(layer, new DicingPreview(0.2, 1, new[] { 0.5, 0.3, 0d }, 0.2)));
        }

        private static (CleanRoomRecipeRole dicing, CleanRoomRecipeRole layer) LoadRoles()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(MasterHolder.CleanRoomMaster.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, out var dicing));
            Assert.IsTrue(MasterHolder.CleanRoomMaster.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, out var layer));
            return (dicing, layer);
        }
    }
}

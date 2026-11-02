using System;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.OutputRewrite;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    public class CleanRoomRoleRatesTest
    {
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");
        private static readonly Guid TestQualityFractionalModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000006");

        [Test]
        public void ClassMultiplierBelowOneLowersTheRareSlotTest()
        {
            // クラスB(倍率0.75)×Tier1モジュール1個: シフト1.0*0.75-1.0=-0.25 でレア枠(0.1)が0になり失敗枠へ戻る
            // Class B (multiplier 0.75) with one tier-1 module: shift 1.0*0.75-1.0=-0.25 drains the 0.1 rare slot back into the failure slot
            var rates = DicingRatesOf(new CleanRoomClassYield(0.75f, 0f, 0f), TestQualityModuleItemGuid, 1, out var qualityTier);
            Assert.AreEqual(1, qualityTier);
            AssertRates(rates, 0.7, 0.0, 0.0, 0.3);
        }

        [Test]
        public void ModuleEffectSumRaisesTheRareSlotUpToTheCapTest()
        {
            // クラスA×Tier2の小数モジュール2個: 合計0.8-基準0.4=+0.4 は maxRareShift と失敗枠で止まる
            // Class A with two tier-2 fractional modules: sum 0.8 minus reference 0.4 = +0.4 is stopped by maxRareShift and the failure slot
            var rates = DicingRatesOf(new CleanRoomClassYield(1f, 0f, 0f), TestQualityFractionalModuleItemGuid, 2, out var qualityTier);
            Assert.AreEqual(2, qualityTier);
            AssertRates(rates, 0.0, 0.7, 0.3, 0.0);
        }

        private static double[] DicingRatesOf(CleanRoomClassYield classYield, Guid moduleItemGuid, int moduleCount, out int qualityTier)
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(MasterHolder.CleanRoomMaster.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, out var role));
            var module = MasterHolder.ItemMaster.GetModuleByItemIdOrNull(MasterHolder.ItemMaster.GetItemId(moduleItemGuid));
            var effect = MachineModuleEffect.Aggregate(new[] { new MachineModuleEffect.EquippedModule(module, moduleCount) });
            return CleanRoomRoleRates.DicingRates((DicingRecipeRole)role, classYield, effect, out qualityTier);
        }

        // 配列は [Lv1, Lv2, Lv3, 失敗ダイ]
        // The array is [Lv1, Lv2, Lv3, failed die]
        private static void AssertRates(double[] actual, params double[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], actual[i], 1e-6, $"index {i}");
        }
    }
}

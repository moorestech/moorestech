using System;
using System.Linq;
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
        public void ClassMultiplierBelowOneSlowsTheApproachTest()
        {
            // クラスB(倍率0.75・損失0)×Tier1モジュール1個: 近づき度 1−exp(−0.75)=0.527633 でモジュール無しの表からTier1の表へ寄る
            // Class B (multiplier 0.75, no loss) with one tier-1 module: weight 1-exp(-0.75)=0.527633 moves the no-module table toward tier 1
            var rates = DicingRatesOf(new CleanRoomClassYield(1, 0.75f, 0f, 0f), TestQualityModuleItemGuid, 1, out var qualityTier);
            Assert.AreEqual(1, qualityTier);
            AssertRates(rates, 0.463817, 0.052763, 0.0, 0.483420);
        }

        [Test]
        public void HighestTierTablePicksLevelsAndEffectSumSetsTheWeightTest()
        {
            // クラスA×Tier2の小数モジュール2個: 合計0.8で近づき度0.550671、表はTier2
            // Class A with two tier-2 fractional modules: sum 0.8 gives weight 0.550671 toward the tier-2 table
            var rates = DicingRatesOf(new CleanRoomClassYield(0, 1f, 0f, 0f), TestQualityFractionalModuleItemGuid, 2, out var qualityTier);
            Assert.AreEqual(2, qualityTier);
            AssertRates(rates, 0.144933, 0.385470, 0.055067, 0.414530);
        }

        private static double[] DicingRatesOf(CleanRoomClassYield classYield, Guid moduleItemGuid, int moduleCount, out int qualityTier)
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(MasterHolder.CleanRoomMaster.TryGetRecipeRole(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, out var role));
            var module = MasterHolder.ItemMaster.GetModuleByItemIdOrNull(MasterHolder.ItemMaster.GetItemId(moduleItemGuid));
            var effect = MachineModuleEffect.Aggregate(new[] { new MachineModuleEffect.EquippedModule(module, moduleCount) });
            var dicing = CleanRoomRoleRates.Dicing((DicingRecipeRole)role, classYield, effect);
            qualityTier = dicing.QualityTier;
            return dicing.LevelRates.Append(dicing.FailedRate).ToArray();
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

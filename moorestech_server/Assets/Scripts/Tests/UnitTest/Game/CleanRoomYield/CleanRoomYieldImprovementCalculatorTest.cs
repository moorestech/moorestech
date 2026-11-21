using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.CleanRoomYield
{
    /// <summary>
    ///     改善の差分（部屋クラスを1つ上げる・品質モジュールを1個足す）の値・並び・変わらない行の除外を固定する
    ///     Pins the improvement deltas (one class up, one more quality module), their order and the dropping of rows that change nothing
    /// </summary>
    public class CleanRoomYieldImprovementCalculatorTest
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void ClassCDicingListsRoomUpThenHighestTierThenNextTierTest()
        {
            var (dicing, _) = LoadRoles();
            var tier1 = QualityModule(1);
            var tier2 = QualityModule(2);

            // クラスC×tier1 1個から、クラスB・tier1 を1個足す・tier2 を1個足す の順に並ぶ
            // From class C with one tier-1 module the rows run: class B, one more tier 1, one tier 2
            var improvements = Compute(dicing, 2, Loadout(tier1, 1), tier1, tier2);
            Assert.AreEqual(3, improvements.Count);
            Assert.AreEqual("B", ((CleanRoomRoomClassUpImprovement)improvements[0]).TargetClassName);
            AssertDeltas(improvements[0], dicing, 2, Loadout(tier1, 1), 1, Loadout(tier1, 1));
            Assert.AreEqual(1, ((CleanRoomAddQualityModuleImprovement)improvements[1]).ModuleTier);
            AssertDeltas(improvements[1], dicing, 2, Loadout(tier1, 1), 2, Loadout(tier1, 2));
            Assert.AreEqual(2, ((CleanRoomAddQualityModuleImprovement)improvements[2]).ModuleTier);
            AssertDeltas(improvements[2], dicing, 2, Loadout(tier1, 1), 2, new[] { new MachineModuleEffect.EquippedModule(tier1, 1), new MachineModuleEffect.EquippedModule(tier2, 1) });
        }

        [Test]
        public void ClassAWithoutModulesListsOnlyTheTier1AdditionTest()
        {
            var (dicing, _) = LoadRoles();
            var tier1 = QualityModule(1);

            // クラスAより上は無いので部屋クラスの行は出ず、tier1 を1個足す行だけになる
            // Nothing is above class A, so no room-class row appears and only the one-tier-1 row remains
            var improvements = Compute(dicing, 0, new List<MachineModuleEffect.EquippedModule>(), tier1);
            Assert.AreEqual(1, improvements.Count);
            Assert.AreEqual(1, ((CleanRoomAddQualityModuleImprovement)improvements[0]).ModuleTier);
            AssertDeltas(improvements[0], dicing, 0, Array.Empty<MachineModuleEffect.EquippedModule>(), 0, Loadout(tier1, 1));
        }

        [Test]
        public void SaturatedLayerExposureListsNothingTest()
        {
            var (_, layer) = LoadRoles();
            var tier1 = QualityModule(1);
            var tier2 = QualityModule(2);

            // EUV のクラスA×tier1 1個は品質合計1.0が低減の上限0.8を超えているので、どれを足しても失敗は変わらない
            // EUV in class A with one tier-1 module already exceeds the 0.8 reduction cap, so no addition changes the failure
            Assert.IsEmpty(Compute(layer, 0, Loadout(tier1, 1), tier1, tier2));
        }

        [Test]
        public void LayerExposureDeltasCarryOnlyTheFailureTest()
        {
            var (_, layer) = LoadRoles();
            var tier1 = QualityModule(1);

            // EUV は部屋と装着それぞれの改善で、失敗の差分だけを持つ
            // EUV improvements for the room and loadout each carry only the failure delta
            var improvements = Compute(layer, 2, new List<MachineModuleEffect.EquippedModule>(), tier1);
            Assert.AreEqual(2, improvements.Count);
            Assert.AreEqual("B", ((CleanRoomRoomClassUpImprovement)improvements[0]).TargetClassName);
            AssertDeltas(improvements[0], layer, 2, Array.Empty<MachineModuleEffect.EquippedModule>(), 1, Array.Empty<MachineModuleEffect.EquippedModule>());
            AssertDeltas(improvements[1], layer, 2, Array.Empty<MachineModuleEffect.EquippedModule>(), 2, Loadout(tier1, 1));
        }

        // 部品と同じく、今の最終の内訳を1回だけ出して計算へ渡す
        // As the component does, derive the current final odds once and hand them to the calculator
        private static List<CleanRoomYieldImprovement> Compute(CleanRoomRecipeRole role, int thresholdIndex, List<MachineModuleEffect.EquippedModule> equipped, params ModuleMasterElement[] candidates)
        {
            var classYield = CleanRoomClassYield.FromThresholdIndex(thresholdIndex);
            var current = CleanRoomFinalOdds.Of(role, CleanRoomYieldPreview.Evaluate(role, classYield, MachineModuleEffect.Aggregate(equipped), null, out _));
            return CleanRoomYieldImprovementCalculator.Compute(role, classYield, equipped, current, candidates);
        }

        private static List<MachineModuleEffect.EquippedModule> Loadout(ModuleMasterElement module, int count)
        {
            return new List<MachineModuleEffect.EquippedModule> { new(module, count) };
        }

        private static void AssertDeltas(CleanRoomYieldImprovement improvement, CleanRoomRecipeRole role, int beforeIndex,
            IReadOnlyList<MachineModuleEffect.EquippedModule> beforeLoadout, int afterIndex, IReadOnlyList<MachineModuleEffect.EquippedModule> afterLoadout)
        {
            // 変更前後の抽選率から最終の内訳を別々に求め、表示の差分を照合する
            // Compare the displayed deltas with final odds independently derived from the before and after draw rates
            var before = OddsAt(beforeIndex, beforeLoadout);
            var after = OddsAt(afterIndex, afterLoadout);
            var levelDeltas = after.LevelRates.Zip(before.LevelRates, (newRate, oldRate) => newRate - oldRate);
            Assert.That(improvement.LevelRateDeltas, Is.EqualTo(levelDeltas).Within(Tolerance));
            Assert.AreEqual(after.FailedRate - before.FailedRate, improvement.FailedRateDelta, Tolerance);
            if (role is DicingRecipeRole) Assert.AreEqual(0d, improvement.LevelRateDeltas.Sum() + improvement.FailedRateDelta, Tolerance);

            #region Internal

            CleanRoomFinalOdds OddsAt(int index, IReadOnlyList<MachineModuleEffect.EquippedModule> loadout)
            {
                var rates = CleanRoomYieldPreview.Evaluate(role, CleanRoomClassYield.FromThresholdIndex(index), MachineModuleEffect.Aggregate(loadout), null, out var outputs);
                Assert.IsNull(outputs);
                return CleanRoomFinalOdds.Of(role, rates);
            }

            #endregion
        }

        private static ModuleMasterElement QualityModule(int tier)
        {
            return MasterHolder.ItemMaster.Items.Modules.Single(module => module.EffectAxis == ModuleMasterElement.EffectAxisConst.Quality && module.Tier == tier);
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

using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Blocks.Machine.State.Util;
using Game.Block.Blocks.Util;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.CombinedTest.Core
{
    // 機械側の確率抽選が世界共有の GameRandom を引くことを、引いた回数ごと固定する
    // Pins the machine-side probabilistic draws to the shared world GameRandom, down to how many draws each takes
    public class MachineRandomDrawSiteTest
    {
        [Test]
        public void サブtickの確率丸めはGameRandomを1回だけ引く()
        {
            var rounded = GameRandomDrawAssert.StateAfterDraws(1);
            GameRandomDrawAssert.BeginDrawCount();
            MachineCurrentPowerToSubSecond.GetSubTicks(1.5f, 1f);
            GameRandomDrawAssert.AssertDrawn(rounded, "サブtickの確率丸めが世界共有の乱数を引いていない");

            // 必要電力0は丸めが起きない経路なので、1回も引いてはならない
            // Zero required power skips the rounding entirely, so it must not draw at all
            var untouched = GameRandomDrawAssert.StateAfterDraws(0);
            GameRandomDrawAssert.BeginDrawCount();
            MachineCurrentPowerToSubSecond.GetSubTicks(1.5f, 0f);
            GameRandomDrawAssert.AssertDrawn(untouched, "丸めの無い経路が乱数を引いている");
        }

        [Test]
        public void 追加産出の抽選はGameRandomを1回だけ引く()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.OutputItems.Length);
            var noModuleEffect = MachineModuleEffect.Aggregate(new List<MachineModuleEffect.EquippedModule>());

            var expected = GameRandomDrawAssert.StateAfterDraws(1);
            GameRandomDrawAssert.BeginDrawCount();
            MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, noModuleEffect);
            GameRandomDrawAssert.AssertDrawn(expected, "追加産出の抽選が世界共有の乱数を引いていない");
        }

        // 品質レベルは1サイクル1回だけ引く。追加産出の抽選と合わせて2回が仕様
        // The quality level is rolled once per cycle; together with the extra-output roll the cycle draws exactly twice
        [Test]
        public void 品質レベルの抽選は追加産出とあわせてGameRandomを2回引く()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.OutputItems.Length);
            var qualityModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Quality);
            var qualityEffect = MachineModuleEffect.Aggregate(new List<MachineModuleEffect.EquippedModule> { new(qualityModule, 1) });
            Assert.Greater(qualityEffect.QualityShift, 0f, "品質モジュールが品質シフトを持っていない（前提が崩れている）");

            var expected = GameRandomDrawAssert.StateAfterDraws(2);
            GameRandomDrawAssert.BeginDrawCount();
            MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, qualityEffect);
            GameRandomDrawAssert.AssertDrawn(expected, "品質レベルの抽選が世界共有の乱数を引いていない");
        }
    }
}

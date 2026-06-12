using System;
using Game.Block.Blocks.Machine.Module;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    ///     MachineModuleEffectの純粋な集計ロジックを検証するテスト（DIコンテナ不要）
    ///     Tests for the pure aggregation logic of MachineModuleEffect (no DI container required)
    /// </summary>
    public class MachineModuleEffectTest
    {
        private const float Delta = 0.0001f;

        // モジュール無しの集計が中立効果（1/1/0）であることを検証
        // Verify aggregation of no modules yields the neutral effect (1/1/0)
        [Test]
        public void EmptyNeutralTest()
        {
            var effect = MachineModuleEffect.Aggregate(Array.Empty<MachineModuleEffect.EquippedModule>());
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0f, effect.ExtraOutputChance, Delta);
        }

        // 速度モジュール2枚が加算で効き、時間半減・電力2倍になることを検証
        // Verify two speed modules stack additively: half time and double power
        [Test]
        public void SpeedAdditiveTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Speed, 0.5f, 0.5f),
                CreateModule(ModuleParam.EffectAxisConst.Speed, 0.5f, 0.5f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            // 時間 = 1/(1+1.0) = 0.5、電力 = (1+1.0)/1 = 2.0
            // Time = 1/(1+1.0) = 0.5, power = (1+1.0)/1 = 2.0
            Assert.AreEqual(0.5f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(2f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0f, effect.ExtraOutputChance, Delta);
        }

        // 生産性モジュール2枚で追加出力確率が1.0にクランプされることを検証
        // Verify two productivity modules clamp the extra output chance to 1.0
        [Test]
        public void ProductivityClampTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Productivity, 1f, 0.5f),
                CreateModule(ModuleParam.EffectAxisConst.Productivity, 1f, 0.5f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            // 追加出力 = clamp(2.0, 0, 1) = 1.0、時間 = (1+1.0)/1 = 2.0
            // Extra output = clamp(2.0, 0, 1) = 1.0, time = (1+1.0)/1 = 2.0
            Assert.AreEqual(1f, effect.ExtraOutputChance, Delta);
            Assert.AreEqual(2f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.PowerMultiplier, Delta);
        }

        // 省エネモジュールが電力を下げ、極端な値では下限0.1にクランプされることを検証
        // Verify efficiency modules lower power and extreme values clamp at the 0.1 floor
        [Test]
        public void EfficiencyAndMinClampTest()
        {
            var single = MachineModuleEffect.Aggregate(new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Efficiency, 0.3f, 0f),
            });
            Assert.AreEqual(1f / 1.3f, single.PowerMultiplier, Delta);
            Assert.AreEqual(1f, single.ProcessingTimeMultiplier, Delta);

            var extreme = MachineModuleEffect.Aggregate(new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Efficiency, 100f, 0f),
            });
            Assert.AreEqual(0.1f, extreme.PowerMultiplier, Delta);
        }

        // 速度＋生産性の混載で各係数が正しく合成されることを検証
        // Verify mixed speed and productivity modules combine each multiplier correctly
        [Test]
        public void MixedAxesTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Speed, 0.5f, 0.5f),
                CreateModule(ModuleParam.EffectAxisConst.Productivity, 1f, 0.5f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            // 時間 = (1+0.5)/(1+0.5) = 1.0、電力 = (1+0.5)/1 = 1.5、追加出力 = 1.0
            // Time = (1+0.5)/(1+0.5) = 1.0, power = (1+0.5)/1 = 1.5, extra output = 1.0
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1.5f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(1f, effect.ExtraOutputChance, Delta);
        }

        // Quality軸は品質シフトと時間延長にのみ効き、電力・追加出力には影響しないことを検証
        // Verify the Quality axis affects only the quality shift and time stretch, not power or extra output
        [Test]
        public void QualityAffectsOnlyShiftAndTimeTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Quality, 0.8f, 0.4f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            Assert.AreEqual(1.4f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0f, effect.ExtraOutputChance, Delta);
            Assert.AreEqual(0.8f, effect.QualityShift, Delta);
        }

        // 品質モジュール2枚の効果値がQualityShiftへ加算で集計されることを検証
        // Verify two quality modules accumulate their effect values into QualityShift additively
        [Test]
        public void QualityShiftAccumulateTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Quality, 0.7f, 0f),
                CreateModule(ModuleParam.EffectAxisConst.Quality, 0.7f, 0f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            // 0.7 + 0.7 = 1.4 → 整数部1段は確定、小数部0.4の確率でもう1段
            // 0.7 + 0.7 = 1.4 → one guaranteed level-up plus a 0.4 chance of one more
            Assert.AreEqual(1.4f, effect.QualityShift, Delta);
        }

        // 品質効果なしではQualityShiftが0（中立）であることを検証
        // Verify QualityShift is 0 (neutral) without any quality effect
        [Test]
        public void QualityNeutralTest()
        {
            var effect = MachineModuleEffect.Aggregate(Array.Empty<MachineModuleEffect.EquippedModule>());
            Assert.AreEqual(0f, effect.QualityShift, Delta);
        }

        // 品質モジュールのトレードオフ値が加工時間を延長することを検証
        // Verify the quality module tradeoff value stretches the processing time
        [Test]
        public void QualityTradeoffExtendsTimeTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Quality, 1f, 0.5f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            // 時間 = (1+0.5)/1 = 1.5、品質シフト = 1.0
            // Time = (1+0.5)/1 = 1.5, quality shift = 1.0
            Assert.AreEqual(1.5f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.QualityShift, Delta);
        }

        // 1スロットにスタックしたモジュールが個数分の効果を持つ（2スロット分載と等価）ことを検証
        // Verify a stacked module slot contributes per item count (equivalent to spreading across two slots)
        [Test]
        public void StackedCountMultipliesEffectTest()
        {
            var stacked = MachineModuleEffect.Aggregate(new[]
            {
                CreateStackedModule(ModuleParam.EffectAxisConst.Speed, 0.5f, 0.5f, 2),
            });
            var spread = MachineModuleEffect.Aggregate(new[]
            {
                CreateModule(ModuleParam.EffectAxisConst.Speed, 0.5f, 0.5f),
                CreateModule(ModuleParam.EffectAxisConst.Speed, 0.5f, 0.5f),
            });

            Assert.AreEqual(spread.ProcessingTimeMultiplier, stacked.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(spread.PowerMultiplier, stacked.PowerMultiplier, Delta);
        }

        // テスト用のモジュール定義（スタック数1）を直接生成する
        // Construct a module definition (stack count 1) directly for tests
        private static MachineModuleEffect.EquippedModule CreateModule(string effectAxis, float effectValue, float tradeoffValue)
        {
            return CreateStackedModule(effectAxis, effectValue, tradeoffValue, 1);
        }

        // テスト用のモジュール定義を指定スタック数で生成する
        // Construct a module definition with the given stack count for tests
        private static MachineModuleEffect.EquippedModule CreateStackedModule(string effectAxis, float effectValue, float tradeoffValue, int count)
        {
            var element = new ModuleParam(effectAxis, 1, effectValue, tradeoffValue);
            return new MachineModuleEffect.EquippedModule(element, count);
        }
    }
}

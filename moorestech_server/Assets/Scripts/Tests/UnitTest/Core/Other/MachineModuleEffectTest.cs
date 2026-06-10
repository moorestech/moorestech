using System;
using Game.Block.Blocks.Machine.Module;
using Mooresmaster.Model.ModulesModule;
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

        // モジュール無しの集計とNeutralが中立効果（1/1/0）であることを検証
        // Verify aggregation of no modules and Neutral both yield the neutral effect (1/1/0)
        [Test]
        public void EmptyNeutralTest()
        {
            var effect = MachineModuleEffect.Aggregate(Array.Empty<ModuleMasterElement>());
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0f, effect.ExtraOutputChance, Delta);

            Assert.AreEqual(1f, MachineModuleEffect.Neutral.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, MachineModuleEffect.Neutral.PowerMultiplier, Delta);
            Assert.AreEqual(0f, MachineModuleEffect.Neutral.ExtraOutputChance, Delta);
        }

        // 速度モジュール2枚が加算で効き、時間半減・電力2倍になることを検証
        // Verify two speed modules stack additively: half time and double power
        [Test]
        public void SpeedAdditiveTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 0.5f, 0.5f),
                CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 0.5f, 0.5f),
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
                CreateModule(ModuleMasterElement.EffectAxisConst.Productivity, 1f, 0.5f),
                CreateModule(ModuleMasterElement.EffectAxisConst.Productivity, 1f, 0.5f),
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
                CreateModule(ModuleMasterElement.EffectAxisConst.Efficiency, 0.3f, 0f),
            });
            Assert.AreEqual(1f / 1.3f, single.PowerMultiplier, Delta);
            Assert.AreEqual(1f, single.ProcessingTimeMultiplier, Delta);

            var extreme = MachineModuleEffect.Aggregate(new[]
            {
                CreateModule(ModuleMasterElement.EffectAxisConst.Efficiency, 100f, 0f),
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
                CreateModule(ModuleMasterElement.EffectAxisConst.Speed, 0.5f, 0.5f),
                CreateModule(ModuleMasterElement.EffectAxisConst.Productivity, 1f, 0.5f),
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
                CreateModule(ModuleMasterElement.EffectAxisConst.Quality, 0.8f, 0.4f),
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
                CreateModule(ModuleMasterElement.EffectAxisConst.Quality, 0.7f, 0f),
                CreateModule(ModuleMasterElement.EffectAxisConst.Quality, 0.7f, 0f),
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
            var effect = MachineModuleEffect.Aggregate(Array.Empty<ModuleMasterElement>());
            Assert.AreEqual(0f, effect.QualityShift, Delta);
            Assert.AreEqual(0f, MachineModuleEffect.Neutral.QualityShift, Delta);
        }

        // 品質モジュールのトレードオフ値が加工時間を延長することを検証
        // Verify the quality module tradeoff value stretches the processing time
        [Test]
        public void QualityTradeoffExtendsTimeTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleMasterElement.EffectAxisConst.Quality, 1f, 0.5f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            // 時間 = (1+0.5)/1 = 1.5、品質シフト = 1.0
            // Time = (1+0.5)/1 = 1.5, quality shift = 1.0
            Assert.AreEqual(1.5f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.QualityShift, Delta);
        }

        // セーブ値からの復元では時間倍率1のまま電力・追加出力・品質シフトのみ復元されることを検証
        // Verify FromSaved restores power, extra output, and quality shift while keeping the time multiplier at 1
        [Test]
        public void FromSavedTest()
        {
            var effect = MachineModuleEffect.FromSaved(1.5f, 0.7f, 1.4f);
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1.5f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0.7f, effect.ExtraOutputChance, Delta);
            Assert.AreEqual(1.4f, effect.QualityShift, Delta);
        }

        // テスト用のモジュール定義を直接生成する
        // Construct a module definition directly for tests
        private static ModuleMasterElement CreateModule(string effectAxis, float effectValue, float tradeoffValue)
        {
            return new ModuleMasterElement(0, Guid.NewGuid(), "TestModule", Guid.NewGuid(), effectAxis, 1, effectValue, tradeoffValue);
        }
    }
}

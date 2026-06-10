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

        // Quality軸はPhase Aでは集計に影響しない（中立のまま）ことを検証
        // Verify the Quality axis does not affect aggregation in Phase A (stays neutral)
        [Test]
        public void QualityIgnoredTest()
        {
            var modules = new[]
            {
                CreateModule(ModuleMasterElement.EffectAxisConst.Quality, 0.8f, 0.4f),
            };
            var effect = MachineModuleEffect.Aggregate(modules);

            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0f, effect.ExtraOutputChance, Delta);
        }

        // セーブ値からの復元では時間倍率1のまま電力・追加出力のみ復元されることを検証
        // Verify FromSaved restores only power and extra output while keeping the time multiplier at 1
        [Test]
        public void FromSavedTest()
        {
            var effect = MachineModuleEffect.FromSaved(1.5f, 0.7f);
            Assert.AreEqual(1f, effect.ProcessingTimeMultiplier, Delta);
            Assert.AreEqual(1.5f, effect.PowerMultiplier, Delta);
            Assert.AreEqual(0.7f, effect.ExtraOutputChance, Delta);
        }

        // テスト用のモジュール定義を直接生成する
        // Construct a module definition directly for tests
        private static ModuleMasterElement CreateModule(string effectAxis, float effectValue, float tradeoffValue)
        {
            return new ModuleMasterElement(0, Guid.NewGuid(), "TestModule", Guid.NewGuid(), effectAxis, 1, effectValue, tradeoffValue);
        }
    }
}

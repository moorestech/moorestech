using System;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    public class GenerationRuntimeConfigMapObjectValidationTest
    {
        [Test]
        public void EmptyMapObjectGuidIsRejectedBeforeMapGeneration()
        {
            // 空GUIDを生成処理へ渡さず、マスタ変換境界で不備を検出する。
            // Detect the master-data gap at the conversion boundary instead of passing an empty GUID into generation.
            var generation = TestGenerationConfigFactory.CreateWithMapObjectGuid(string.Empty);

            Assert.Throws<InvalidOperationException>(() => GenerationRuntimeConfigFactory.Build(generation));
        }
    }
}

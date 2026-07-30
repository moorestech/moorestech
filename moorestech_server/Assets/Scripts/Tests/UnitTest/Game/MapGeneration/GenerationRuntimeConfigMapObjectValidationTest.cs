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
            var generation = TestGenerationConfigFactory.CreateWithMapObjectGuid(string.Empty);

            Assert.Throws<InvalidOperationException>(() => GenerationRuntimeConfigFactory.Build(generation));
        }
    }
}

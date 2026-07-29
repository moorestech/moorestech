using Client.Game.InGame.Environment.Terrain.Build;
using Game.MapGeneration.Pipeline.Biomes;
using NUnit.Framework;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     Detail配置マスクが転送バイオームだけを見ていることを検証する。海やビーチが漏れると水面下に草が生え、
    ///     隣のバイオームが漏れると別バイオームの草が混ざる
    ///     Verifies the detail placement mask reads only the transferred biome; leaking sea or beach grows grass
    ///     underwater, and leaking a neighbouring biome mixes in that biome's plants
    /// </summary>
    public class TransferredBiomeMaskBuilderTest
    {
        private const int Resolution = 2;

        [Test]
        public void SelectsOnlyThePixelsHoldingTheRequestedBiome()
        {
            var transferredBiomeIndices = new byte[Resolution, Resolution];
            transferredBiomeIndices[0, 0] = (byte)BiomeType.Ocean;
            transferredBiomeIndices[0, 1] = (byte)BiomeType.Beach;
            transferredBiomeIndices[1, 0] = (byte)BiomeType.Grassland;
            transferredBiomeIndices[1, 1] = (byte)BiomeType.Forest;

            var mask = TransferredBiomeMaskBuilder.Build(transferredBiomeIndices, BiomeType.Grassland, Resolution);

            Assert.That(mask[1, 0], Is.True, "Grassland のピクセルだけがtrue");
            Assert.That(mask[0, 0], Is.False, "Ocean は陸のDetail対象外");
            Assert.That(mask[0, 1], Is.False, "Beach は陸のDetail対象外");
            Assert.That(mask[1, 1], Is.False, "Forest は別バイオーム");
        }

        [Test]
        public void KeepsTheZxAxisOrderOfTheTransferredData()
        {
            // [z, x]を取り違えるとマスクが転置され、Detailが地形と鏡像の位置に生える
            // Swapping [z, x] transposes the mask, growing details mirrored against the terrain
            var transferredBiomeIndices = new byte[Resolution, Resolution];
            transferredBiomeIndices[0, 1] = (byte)BiomeType.Desert;

            var mask = TransferredBiomeMaskBuilder.Build(transferredBiomeIndices, BiomeType.Desert, Resolution);

            Assert.That(mask[0, 1], Is.True);
            Assert.That(mask[1, 0], Is.False);
        }
    }
}

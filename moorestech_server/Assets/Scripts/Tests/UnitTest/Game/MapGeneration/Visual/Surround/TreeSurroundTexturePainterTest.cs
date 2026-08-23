using System;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Surround;
using NUnit.Framework;
using UnityEngine;
using static Tests.UnitTest.Game.MapGeneration.Visual.Surround.SurroundTestFixtures;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Surround
{
    /// <summary>
    ///     木の根元の塗りを検証する。畳み方が岩のSurroundBlendWriter（元の合計を掛けて足す）に化けると根元の濃さだけが
    ///     静かに変わるので、重みが残っている画素の期待値を数値で固定して見分ける。
    ///     ガウシアンの減衰・打ち切り半径・guidマップの引き分けも、どれが欠けても落ちるように置いてある
    ///     Checks the painting under a tree's root. Swapping the fold for the rocks' SurroundBlendWriter, which adds the
    ///     blended share of the original total, would quietly change only the root's strength, so the expected values on a
    ///     pixel that already carries weight are pinned numerically.
    ///     The Gaussian falloff, the cutoff radius and the guid lookup are each placed so that dropping any one of them fails
    /// </summary>
    public class TreeSurroundTexturePainterTest
    {

        [SetUp]
        public void SetUp()
        {
            LoadMasterData();
        }
        // 種別はマスタが正本になったので、木・岩ともforUnitTestマスタに実在するguidを使う
        // The master now owns the kind, so both the tree and the rock use guids the forUnitTest master really defines
        private const string TreeGuid = SurroundTestFixtures.TreeGuid;
        private const string RockGuid = SurroundTestFixtures.StoneGuid;
        private const string TreeLayerAddress = "addr/Mud01";

        private const float Weight = 0.8f;

        // 40mを8画素で割ると1画素40/7m。幅をその2画素ぶんに取ると radiusPixels がちょうど2になる
        // A 40m tile over 8 pixels spans 40/7m each, and a width of two such pixels makes radiusPixels exactly 2
        private const float Width = TerrainSize * 2f / (AlphaResolution - 1);
        private const int TreePixel = RockPixel;

        // sigma=radiusPixels/3 なので中心から1画素の減衰は exp(-1/(2*(2/3)^2))。sigmaを変えるとここが動く
        // With sigma = radiusPixels/3 the falloff one pixel out is exp(-1/(2*(2/3)^2)), and any other sigma moves it
        private const float FalloffAtOnePixel = 0.32465247f;

        [Test]
        public void ATreePullsThePixelUnderItsRootOntoItsSurroundLayer()
        {
            var alphamap = CreateUniformAlphamap();
            Paint(alphamap, CreateTree(TreeGuid), CreateSpecies(TreeLayerAddress, Weight, Width));

            // 中心は減衰1なので blend=weight。書き込み先は 0*(1-w)+w、他は元の重み*(1-w)
            // The falloff is 1 at the centre so blend = weight: the target lands on 0*(1-w)+w and the rest on their weight*(1-w)
            Assert.That(alphamap[TreePixel, TreePixel, MudLayerIndex], Is.EqualTo(Weight).Within(1e-4f));
            Assert.That(alphamap[TreePixel, TreePixel, 2], Is.EqualTo(1f - Weight).Within(1e-4f));
        }

        [Test]
        public void APixelWhoseSurroundLayerAlreadyHasWeightKeepsItsSumAtOne()
        {
            // 木は再正規化しない代わりに元の合計も掛けない。合計1の盤面は合計1のまま残る
            // A tree neither renormalizes nor multiplies by the original total, so a board summing to 1 still sums to 1
            var alphamap = CreateAlphamapWithSurroundWeight();
            Paint(alphamap, CreateTree(TreeGuid), CreateSpecies(TreeLayerAddress, Weight, Width));

            // 岩のSurroundBlendWriterに化けると書き込み先は 0.4+0.8*1.0=1.2 になり、合計も1.32へ膨らむ
            // Under the rocks' SurroundBlendWriter the target would be 0.4 + 0.8*1.0 = 1.2 and the sum would swell to 1.32
            Assert.That(alphamap[TreePixel, TreePixel, MudLayerIndex],
                Is.EqualTo(PresetSurroundWeight * (1f - Weight) + Weight).Within(1e-4f));
            Assert.That(alphamap[TreePixel, TreePixel, 2], Is.EqualTo(PresetMainWeight * (1f - Weight)).Within(1e-4f));

            var sum = 0f;
            for (var layer = 0; layer < LayerCount; layer++) sum += alphamap[TreePixel, TreePixel, layer];
            Assert.That(sum, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TheBlendFadesAsAGaussianAndStopsAtTheRadius()
        {
            var alphamap = CreateUniformAlphamap();
            Paint(alphamap, CreateTree(TreeGuid), CreateSpecies(TreeLayerAddress, Weight, Width));

            // 1画素隣はガウシアンぶんだけ薄い。線形減衰へ差し替えると 0.4 になりここで落ちる
            // One pixel out is thinner by the Gaussian; a linear falloff would read 0.4 and fail here
            Assert.That(alphamap[TreePixel, TreePixel + 1, MudLayerIndex],
                Is.EqualTo(Weight * FalloffAtOnePixel).Within(1e-4f));

            // 半径ちょうどの2画素までは塗り、その外は一切触らない
            // Painting reaches the two-pixel radius exactly and never touches anything past it
            Assert.That(alphamap[TreePixel, TreePixel + 2, MudLayerIndex], Is.GreaterThan(0f));
            Assert.That(alphamap[TreePixel, TreePixel + 3, MudLayerIndex], Is.EqualTo(0f));

            // 斜め(1,2)は距離√5で半径の外。距離判定を落とすと走査の正方形がそのまま塗られる
            // The (1, 2) diagonal sits at √5, outside the radius; dropping the distance test would paint the scan square whole
            Assert.That(alphamap[TreePixel + 1, TreePixel + 2, MudLayerIndex], Is.EqualTo(0f));
        }

        [Test]
        public void ZIsNormalizedByTheTerrainLengthRatherThanItsWidth()
        {
            // 正方形タイルでは差が出ない。長辺の違うタイルで初めて、Zを幅で割る実装が格子外を指して無反応になる
            // A square tile hides the difference; only on a tile whose length differs does dividing Z by the width point off the lattice
            var config = CreateConfig();
            config.terrainLength = TerrainSize * 2f;

            var alphamap = CreateUniformAlphamap();
            var tree = CreateTreeAt(TreeGuid, RockLocalPosition, RockLocalPosition * 2f);
            TreeSurroundTexturePainter.Apply(
                alphamap, config, CreateLayerTable(), CreateSpecies(TreeLayerAddress, Weight, Width),
                new[] { tree }, Vector3.zero);

            Assert.That(alphamap[TreePixel, TreePixel, MudLayerIndex], Is.EqualTo(Weight).Within(1e-4f));
        }

        [Test]
        public void APrototypeWithNoSurroundLayerOrNoWeightPaintsNothing()
        {
            // 木には岩のようなMudフォールバックが無い。未設定を勝手に倒すと全樹種の根元が塗られてしまう
            // A tree has no Mud fallback as the rocks do; falling an unset layer back would paint the root of every species
            var unsetAlphamap = CreateUniformAlphamap();
            Paint(unsetAlphamap, CreateTree(TreeGuid), CreateSpecies(string.Empty, Weight, Width));

            var zeroWeightAlphamap = CreateUniformAlphamap();
            Paint(zeroWeightAlphamap, CreateTree(TreeGuid), CreateSpecies(TreeLayerAddress, 0f, Width));

            Assert.That(unsetAlphamap[TreePixel, TreePixel, MudLayerIndex], Is.EqualTo(0f));
            Assert.That(zeroWeightAlphamap[TreePixel, TreePixel, MudLayerIndex], Is.EqualTo(0f));
        }

        [Test]
        public void AWeightWithNoWidthIsRejectedRatherThanSprayingNaN()
        {
            // 幅0はsigma0でガウシアンがNaNになる。飛ばして黙らせると根元どころかタイル全面の重みが壊れる
            // A zero width makes sigma zero and the Gaussian NaN; skipping it quietly would wreck the whole tile's weights, not just the root
            var alphamap = CreateUniformAlphamap();

            Assert.Throws<InvalidOperationException>(
                () => Paint(alphamap, CreateTree(TreeGuid), CreateSpecies(TreeLayerAddress, Weight, 0f)));
        }

        [Test]
        public void AMapObjectOutsideTheGuidMapPaintsNothing()
        {
            // 岩や鉱脈のguidは木のマップに載らない。引きを飛ばすと岩の周りまで樹種のレイヤーで塗られる
            // Rock and vein guids never enter the tree map; skipping the lookup would paint the species' layer around rocks too
            var alphamap = CreateUniformAlphamap();
            Paint(alphamap, CreateTree(RockGuid), CreateSpecies(TreeLayerAddress, Weight, Width));

            Assert.That(alphamap[TreePixel, TreePixel, MudLayerIndex], Is.EqualTo(0f));
        }

        private static TreeSurroundSpeciesTable CreateSpecies(string layerAddress, float weight, float width)
        {
            return CreateTreeSurroundSpecies(CreateTreePrototype(new[] { TreeGuid }, layerAddress, weight, width));
        }

        private static LedgerPlacement CreateTree(string mapObjectGuid)
        {
            return CreateTreeAt(mapObjectGuid, RockLocalPosition, RockLocalPosition);
        }

        private static LedgerPlacement CreateTreeAt(string mapObjectGuid, float localX, float localZ)
        {
            return new LedgerPlacement(mapObjectGuid, new Vector3(localX, 0f, localZ),
                Vector3.one, TerrainSurroundEffectType.treeRootPatch, null);
        }

        private static void Paint(
            float[,,] alphamap, LedgerPlacement treeObject, TreeSurroundSpeciesTable speciesTable)
        {
            TreeSurroundTexturePainter.Apply(
                alphamap, CreateConfig(), CreateLayerTable(), speciesTable, new[] { treeObject }, Vector3.zero);
        }
    }
}

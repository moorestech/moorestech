using System;
using Game.MapGeneration.Surface;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.Paths;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Visual.Detail;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    /// <summary>
    ///     generateTexture / generateDetail / generateHeightmap が見た目の再構築を切ることを検証する。detailはプロトタイプと密度マップが
    ///     必ず同数でなければならず、片方だけ止めた実装はDetailPrototypeAssetResolverの数一致検査で落ちる
    ///     Verifies generateTexture, generateDetail, and generateHeightmap gate the visual rebuild; detail prototypes and density maps must
    ///     always match in count, and gating only one of them trips DetailPrototypeAssetResolver's count check
    /// </summary>
    public class TileVisualBakerGateTest : TileVisualBakerGateFixture
    {
        [Test]
        public void AppliesTheTransferredHeightsWhenTheHeightmapFlagIsOn()
        {
            // 全画素0xFFFF=正規化高さ1.0。木の摂動が無いEmptyLedgerでは転送値がそのまま表示へ通る
            // Every pixel is 0xFFFF, normalized height 1.0; with EmptyLedger no tree perturbation runs, so the transferred value passes straight through
            WriteMaxHeightFile();
            var baked = CreateBaker(true, true, true).Bake(TileX, TileZ);

            Assert.That(baked.DisplayHeights[4, 4], Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void LeavesTheTerrainFlatWhenTheHeightmapFlagIsOff()
        {
            // 転送値は1.0だが平坦配列に差し替わることだけを見る。生成データ側は変わらず読める
            // The transferred value is 1.0 but only the flat replacement should reach display; the generation-side data itself is unaffected
            WriteMaxHeightFile();
            var baked = CreateBaker(true, true, false).Bake(TileX, TileZ);

            Assert.That(baked.DisplayHeights[4, 4], Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BuildsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOn()
        {
            var baker = CreateBaker(true, true, true);
            var baked = baker.Bake(TileX, TileZ);

            Assert.That(baked.DetailMaps.Count, Is.EqualTo(baker.DetailPrototypes.Count), "プロトタイプと密度マップは同数");
            Assert.That(baker.DetailPrototypes.Count, Is.EqualTo(1));
        }

        [Test]
        public void DropsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOff()
        {
            var baker = CreateBaker(true, false, true);
            var baked = baker.Bake(TileX, TileZ);

            // 片方だけ止めるとDetailPrototypeAssetResolverの数一致検査で落ちる。同数であることが本体の要求
            // Gating only one side trips DetailPrototypeAssetResolver's count check; matching counts are what production demands
            Assert.That(baked.DetailMaps.Count, Is.EqualTo(baker.DetailPrototypes.Count), "プロトタイプと密度マップは同数");
            Assert.That(baker.DetailPrototypes.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuildsTheAlphamapWhenTextureGenerationIsOn()
        {
            var baked = CreateBaker(true, true, true).Bake(TileX, TileZ);

            Assert.That(baked.Alphamap, Is.Not.Null);
            Assert.That(baked.Alphamap.LayerCount, Is.GreaterThan(0));
            Assert.That(baked.Alphamap.Resolution, Is.EqualTo(AlphamapResolution));
        }

        [Test]
        public void LeavesTheAlphamapUnbuiltWhenTextureGenerationIsOff()
        {
            var baked = CreateBaker(false, true, true).Bake(TileX, TileZ);

            // alphamapが無いことがSplatmapRuntimeGenerateを通っていない唯一の観測点
            // The absent alphamap is the single observable telling SplatmapRuntimeGenerator never ran
            Assert.That(baked.Alphamap, Is.Null);
            Assert.That(baked.DetailMaps.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReusesTheCachedVisualOnASecondBakeWhenTextureGenerationIsOn()
        {
            var baker = CreateBaker(true, true, true);
            baker.Bake(TileX, TileZ);

            var cacheFilePath = _worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ);
            Assert.That(File.Exists(cacheFilePath), Is.True);
            var writeTimeAfterFirstBake = File.GetLastWriteTimeUtc(cacheFilePath);

            baker.Bake(TileX, TileZ);

            // ヒットは書き戻さない。更新時刻が動いていないことがヒットの証拠になる
            // A hit never writes back, so an unmoved timestamp is the evidence of the hit
            Assert.That(File.GetLastWriteTimeUtc(cacheFilePath), Is.EqualTo(writeTimeAfterFirstBake));
        }

        [Test]
        public void NeitherReadsNorWritesTheCacheWhenTextureGenerationIsOff()
        {
            // キャッシュ形式はalphamapを必ず1枚要求する。テクスチャ無しの見た目は書き出せない
            // The cache format always demands one alphamap, so a texture-less visual cannot be written at all
            var baker = CreateBaker(false, true, true);
            baker.Bake(TileX, TileZ);
            baker.Bake(TileX, TileZ);

            Assert.That(File.Exists(_worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ)), Is.False);
        }

        [Test]
        public void SkipsHeightAndLedgerWhenEveryGenerationGateIsOff()
        {
            File.Delete(_worldCacheDirectory.TerrainHeightFilePath(TileX, TileZ));
            var ledgerSource = new CountingLedgerSource(EmptyLedger);

            var baked = CreateBaker(false, false, false, ledgerSource, EmptyLedger.ComputeDigest()).Bake(TileX, TileZ);

            Assert.That(ledgerSource.ResolveCount, Is.EqualTo(0));
            Assert.That(baked.DisplayHeights[0, 0], Is.EqualTo(0f));
            Assert.That(baked.Alphamap, Is.Null);
            Assert.That(baked.DetailMaps, Is.Empty);
        }

        [Test]
        public void RejectsResolvedLedgerWhoseDigestDiffersFromTheCacheIdentity()
        {
            var baker = CreateBaker(true, false, true,
                new MaterializedPlacementLedgerSource(EmptyLedger), new string('f', 64));

            LogAssert.Expect(LogType.Error, $"[TileVisualBaker] Resolved placement ledger digest '{EmptyLedger.ComputeDigest()}' does not match expected digest '{new string('f', 64)}'.");
            Assert.Throws<InvalidOperationException>(() => baker.Bake(TileX, TileZ));
            Assert.That(File.Exists(_worldCacheDirectory.TerrainVisualCacheFilePath(TileX, TileZ)), Is.False);
        }

        [Test]
        public void FullCacheHitDoesNotResolveTheLedger()
        {
            CreateBaker(true, false, true).Bake(TileX, TileZ);
            var ledgerSource = new CountingLedgerSource(EmptyLedger);

            CreateBaker(true, false, true, ledgerSource, EmptyLedger.ComputeDigest()).Bake(TileX, TileZ);

            Assert.That(ledgerSource.ResolveCount, Is.EqualTo(0));
        }

        [Test]
        public void TwoDistinctCacheMissesOnTheSameBakerResolveTheLedgerOnceTest()
        {
            const int secondTileX = TileX + 1;
            File.WriteAllBytes(
                _worldCacheDirectory.TerrainHeightFilePath(secondTileX, TileZ),
                new byte[Resolution * Resolution * 2]);
            var ledgerSource = new CountingLedgerSource(EmptyLedger);
            var baker = CreateBaker(true, false, true, ledgerSource, EmptyLedger.ComputeDigest());

            baker.Bake(TileX, TileZ);
            baker.Bake(secondTileX, TileZ);

            Assert.That(ledgerSource.ResolveCount, Is.EqualTo(1));
        }

    }
}

using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Client.Tests.UnitTest.Terrain.Surround;
using NUnit.Framework;

namespace Client.Tests.UnitTest.Terrain.Splat
{
    /// <summary>
    ///     台地デバッグオーバーレイ用のレイヤーが末尾に並ぶことを検証する。PlateauDebugOverlayJobは
    ///     DebugLayerStartからの連番でしか列を指せないので、途中へ挟まると通常レイヤーが台地色に塗り潰される
    ///     Verifies the plateau debug overlay's layers sit at the tail: PlateauDebugOverlayJob can only address columns
    ///     running on from DebugLayerStart, so inserting them earlier repaints ordinary layers with the plateau colour
    /// </summary>
    public class SplatLayerTableDebugLayerTest
    {
        private const int NonDebugLayerCount = 5;

        [Test]
        public void AppendsDebugLayersAfterEveryOtherLayer()
        {
            var table = Build("addr/debug0", "addr/debug1");

            Assert.That(table.OrderedLayerAddresses, Is.EqualTo(new[]
            {
                "addr/beach", "addr/rock", "addr/grass", "addr/mud", "addr/dirt", "addr/debug0", "addr/debug1",
            }));
            Assert.That(table.DebugLayerStart, Is.EqualTo(NonDebugLayerCount));
            Assert.That(table.DebugLayerCount, Is.EqualTo(2));
        }

        [Test]
        public void LeavesEveryOrdinaryLayerOnTheColumnItHadWithoutDebugLayers()
        {
            // 既存の列がずれると過去タイルの見た目キャッシュと列の意味が食い違う
            // A shifted column makes the previous tiles' visual cache disagree about what each column means
            var withoutDebug = Build();
            var withDebug = Build("addr/debug0");

            Assert.That(withoutDebug.OrderedLayerAddresses.Count, Is.EqualTo(NonDebugLayerCount));
            for (var layer = 0; layer < NonDebugLayerCount; layer++)
                Assert.That(withDebug.OrderedLayerAddresses[layer], Is.EqualTo(withoutDebug.OrderedLayerAddresses[layer]));

            Assert.That(withoutDebug.DebugLayerCount, Is.EqualTo(0));
            Assert.That(withDebug.DebugLayerStart, Is.EqualTo(withoutDebug.OrderedLayerAddresses.Count));
        }

        [Test]
        public void KeepsADebugLayerInItsOwnColumnWhenItRepeatsAnEarlierAddress()
        {
            // 通常レイヤーの重複畳みをデバッグ列にも掛けると領域IDと列の対応が縮み、隣り合う台地が同じ色になる
            // Folding duplicates as ordinary layers do would shrink the region-to-column mapping and colour neighbouring plateaus alike
            var table = Build("addr/grass");

            Assert.That(table.OrderedLayerAddresses.Count, Is.EqualTo(NonDebugLayerCount + 1));
            Assert.That(table.DebugLayerStart, Is.EqualTo(NonDebugLayerCount));
            Assert.That(table.DebugLayerCount, Is.EqualTo(1));

            // 索引辞書は通常レイヤーの持ち物。デバッグ列に奪われるとバイオームのメインテクスチャが台地色になる
            // The index dictionary belongs to the ordinary layers; letting a debug column steal it would paint a biome's main texture as a plateau
            Assert.That(table.LayerIndexByAddress["addr/grass"], Is.EqualTo(2));
        }

        [Test]
        public void TreatsEveryUnassignedDebugSlotAsNoColumnAtAll()
        {
            // 実マスタ(generation.json)のalpineは空文字5本を並べた未割当スロット。ここで落とすと地形構築が丸ごと死ぬ
            // The real master's alpine holds five empty placeholder slots; throwing here would kill the whole terrain build
            var table = Build(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

            Assert.That(table.OrderedLayerAddresses.Count, Is.EqualTo(NonDebugLayerCount));
            Assert.That(table.DebugLayerCount, Is.EqualTo(0));
        }

        [Test]
        public void CountsOnlyTheDebugSlotsThatActuallyGotAColumn()
        {
            // 設定の本数で数えると領域IDの剰余が存在しない列を指し、PlateauDebugOverlayJobが塗りを丸ごと捨てる
            // Counting the configured entries would point the region-id modulo at columns that do not exist, dropping the paint entirely
            var table = Build(string.Empty, "addr/debug0", string.Empty);

            Assert.That(table.OrderedLayerAddresses, Is.EqualTo(new[]
            {
                "addr/beach", "addr/rock", "addr/grass", "addr/mud", "addr/dirt", "addr/debug0",
            }));
            Assert.That(table.DebugLayerStart, Is.EqualTo(NonDebugLayerCount));
            Assert.That(table.DebugLayerCount, Is.EqualTo(1));
        }

        // 岩surroundと木の根元まで含めた本番同形の並び。デバッグ列はこの5本の後ろに来なければならない
        // Production-shaped ordering down to the rock surround and tree roots; the debug columns must land behind those five
        private static SplatLayerTable Build(params string[] debugLayerAddresses)
        {
            var treePrototype = SurroundTestFixtures.CreateTreePrototype(
                new[] { "00000000-0000-0000-0000-000000000001" }, "addr/dirt", 1f, 1f);

            return SplatLayerTable.Build(
                "addr/beach", "addr/rock", new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { new SurroundTextureConfig { surroundLayerAddressablePath = "addr/mud" } },
                SurroundTestFixtures.CreateTreeSurroundSpecies(treePrototype), debugLayerAddresses);
        }
    }
}

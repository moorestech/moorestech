using System;
using System.Collections.Generic;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Tiling;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     タイル1枚ぶんの見た目を焼く唯一の窓口。キャッシュの引き当て・再構築・書き戻しをここへ集め、
    ///     detailのプロトタイプ設定と密度マップを同じ持ち主に揃えて片方だけが欠ける形を作れなくする
    ///     The single window baking one tile's visuals, gathering cache lookup, rebuild and write-back;
    ///     the detail prototype configs and density maps share one owner so neither can go missing without the other
    /// </summary>
    public class TileVisualBaker
    {
        private readonly BiomeType[] _biomeTypes;
        private readonly TerrainGenerationConfig _gridConfig;
        private readonly SplatLayerTable _layerTable;
        private readonly PlacementLedger _ledger;
        private readonly TreeSurroundSpeciesTable _treeSurroundSpecies;
        private readonly TerrainVisualCache _visualCache;
        private readonly BiomeVisualSections _visualSections;
        private readonly WorldDataDirectory _heightSource;

        // detailプロトタイプの並びはタイルに依らず一度だけ決める
        // The detail prototype order does not vary by tile and is decided once
        public IReadOnlyList<DetailPrototypeRuntimeConfig> DetailPrototypes { get; }

        public TileVisualBaker(
            TerrainGenerationConfig gridConfig, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
            SplatLayerTable layerTable, TreeSurroundSpeciesTable treeSurroundSpecies, PlacementLedger ledger,
            WorldDataDirectory heightSource, TerrainVisualCache visualCache)
        {
            _gridConfig = gridConfig;
            _biomeTypes = biomeTypes;
            _visualSections = visualSections;
            _layerTable = layerTable;
            _treeSurroundSpecies = treeSurroundSpecies;
            _ledger = ledger;
            _heightSource = heightSource;
            _visualCache = visualCache;

            AssignTextureFilterLayerIndices();

            // プロトタイプ設定と密度マップは同じフラグで生死を共にする。片方だけ残すと本数が食い違ってDetailPrototypesを読む側が壊れる
            // Configs and density maps live and die by one flag; keeping either alone breaks the counts for whoever reads DetailPrototypes
            DetailPrototypes = gridConfig.generateDetail
                ? DetailPrototypeRuntimeConfigCollector.Collect(biomeTypes, visualSections)
                : new List<DetailPrototypeRuntimeConfig>();

            #region Internal

            // textureFilterはアドレスしか知らない。列番号はSplatLayerTableが確定した後でしか分からないため、ここで一括して差し込む
            // A textureFilter knows only its address; the column index is unknowable before SplatLayerTable settles, so it is injected here in one pass
            void AssignTextureFilterLayerIndices()
            {
                foreach (var detailConfig in visualSections.DetailConfigs)
                foreach (var entry in detailConfig.entries)
                {
                    var textureFilter = entry.textureFilter;
                    if (!textureFilter.enabled || textureFilter.entries == null) continue;

                    foreach (var filterEntry in textureFilter.entries)
                    {
                        if (!layerTable.LayerIndexByAddress.TryGetValue(filterEntry.layerAddressablePath, out var layerIndex))
                            throw new InvalidOperationException(
                                $"[TileVisualBaker] Detail texture filter layer '{filterEntry.layerAddressablePath}' is not registered in the splatmap layer table.");

                        filterEntry.SetLayerIndex(layerIndex);
                    }
                }
            }

            #endregion
        }

        public TileVisualBakeResult Bake(int tileX, int tileZ)
        {
            var tileConfig = _gridConfig.CreateTileConfig(tileX, tileZ);
            var tileScene = _gridConfig.TileScenePosition(tileX, tileZ);
            var tileWorldPosition = new Vector3(tileScene.x, 0f, tileScene.y);

            // 転送された高さは木の摂動前が正本（R12）。splatとdetail密度はこの値を読み、表示だけが摂動後を使う
            // The transferred heights are pre-tree by definition (R12): splat and detail density read them and only the display uses the perturbed ones
            var preHeights = HeightFileLoader.LoadHeights(_heightSource, tileX, tileZ, _gridConfig.Resolution);
            var postHeights = TreePerturbationApplier.Apply(preHeights, tileConfig, tileWorldPosition, _ledger.Placements);

            var tileVisual = ResolveVisual();

            // generateTexture/generateDetailと同型の内側ゲート。offなら平坦配列を表示用に渡し、地形本体の起伏を止める
            // The same inner gate shape as generateTexture/generateDetail; off feeds a flat array so the terrain itself stays flat
            var displayHeights = _gridConfig.generateHeightmap ? postHeights : CreateFlatHeights(_gridConfig.Resolution);
            return new TileVisualBakeResult(tileWorldPosition, displayHeights, tileVisual.Alphamap, tileVisual.DetailMaps);

            #region Internal

            // splatもdetailも作らない設定では分類の結果を誰も読まない。パディング窓を回さずに空で返す
            // With neither splat nor detail requested nobody reads the classification, so the padded window is never run
            TerrainTileVisual ResolveVisual()
            {
                if (!_gridConfig.generateTexture && !_gridConfig.generateDetail)
                    return new TerrainTileVisual(null, Array.Empty<int[,]>());

                // キャッシュ形式はalphamapを必ず1枚要求する。テクスチャを作らない見た目は書けないので読みも書きもしない
                // The cache format always demands one alphamap, so a texture-less visual is neither read nor written
                if (!_gridConfig.generateTexture) return Rebuild();

                // detailの解像度とプロトタイプ数を先に固定する。ヒット後に数違いで落とさず、Readerで壊れた取り逃しにする
                // Fix detail resolution and prototype count before loading so a mismatch becomes a broken miss in the Reader, never a post-hit failure
                var cacheHit = _visualCache.TryLoad(
                    tileX, tileZ, _gridConfig.AlphamapResolution, _layerTable.OrderedLayerAddresses.Count, _gridConfig.Resolution - 1,
                    DetailPrototypes.Count, out var cachedTileVisual);
                if (cacheHit) return cachedTileVisual;

                // 取り逃しタイルのみ作り直し書き戻す
                // Only the missed tiles are rebuilt and written back
                var rebuilt = Rebuild();
                _visualCache.Save(tileX, tileZ, rebuilt);
                return rebuilt;

                #region Internal

                TerrainTileVisual Rebuild()
                {
                    // 分類はタイル1枚につき1回。splatのブレンド入力とDetailの勝者マスクを同じパディング窓から採る
                    // One classification per tile, so splat's blend inputs and detail's winner masks come from the same padded window
                    using var classification = new TileClassificationContext(tileConfig, _biomeTypes);
                    classification.Initialize();

                    // 移植元はSplatmapJobそのものをフラグで飛ばす（TerrainGenerator.cs:792）。alphamapが無ければDetailのテクスチャフィルタも休む
                    // The source gates the SplatmapJob itself (TerrainGenerator.cs:792); with no alphamap the detail texture filter idles too
                    var alphamap = _gridConfig.generateTexture ? BuildAlphamap(classification) : null;
                    var detailMaps = _gridConfig.generateDetail
                        ? BuildDetailMaps(classification, alphamap)
                        : new List<int[,]>();

                    // Detailは移植元と同じ生の重みを読む。畳むのはその後で、保存と適用に回る値だけをキャッシュ往復で不変にする
                    // Detail reads the same raw weights as the source; the fold comes after it and only makes the stored, applied values survive a round trip
                    if (alphamap != null) StoredAlphamapWeights.Fold(alphamap);

                    return new TerrainTileVisual(alphamap, detailMaps);
                }

                // splatも岩の裸地でmapObjectを読むようになったので、Detailと同じく全タイルぶんを渡してhaloで切らせる
                // The splat now reads map objects for the rocks' bare ground too, so it takes the whole layout and slices its own halo, as detail does
                float[,,] BuildAlphamap(TileClassificationContext classification)
                {
                    var resolution = _gridConfig.Resolution;

                    // 勝者の確定はTileBiomeIndexBuilderが唯一の場所で、splatが読む[z,x]の形で返ってくる
                    // TileBiomeIndexBuilder is the single place settling the winner and hands it back in the [z,x] shape the splat reads
                    var biomeIndices = TileBiomeIndexBuilder.BuildTileGrid(classification.Buffers, _biomeTypes, resolution);

                    return SplatmapStage.Generate(
                        tileConfig, _biomeTypes, classification, _layerTable, _visualSections, _treeSurroundSpecies,
                        preHeights, biomeIndices, _gridConfig.AlphamapResolution,
                        _ledger.Placements, tileWorldPosition);
                }

                // 距離場はタイル境界の外まで見るため、切り出し済みのタイル内mapObjectではなく全タイルぶんを渡す
                // The distance fields look past the tile boundary, so the whole layout goes in rather than the tile's own slice
                List<int[,]> BuildDetailMaps(TileClassificationContext classification, float[,,] alphamap)
                {
                    return TerrainDetailBuilder.Build(
                        tileConfig, _biomeTypes, _visualSections, preHeights, postHeights, classification.WinnerMasks,
                        alphamap, _ledger.Placements, tileWorldPosition, tileX, tileZ);
                }

                #endregion
            }

            float[,] CreateFlatHeights(int resolution)
            {
                return new float[resolution, resolution];
            }

            #endregion
        }
    }
}

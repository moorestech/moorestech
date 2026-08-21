using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     タイル1枚ぶんの見た目を配る唯一の窓口。キャッシュの引き当て・再構築・書き戻しをここへ集め、
    ///     detailのプロトタイプと密度マップを同じ持ち主に揃えて片方だけが欠ける形を作れなくする
    ///     The single window handing out one tile's visuals, gathering cache lookup, rebuild and write-back;
    ///     the detail prototypes and density maps share one owner so neither can go missing without the other
    /// </summary>
    public class TerrainTileVisualProvider
    {
        private readonly BiomeType[] _biomeTypes;
        private readonly TerrainGenerationConfig _config;
        private readonly SplatLayerTable _layerTable;
        private readonly IReadOnlyList<LedgerPlacement> _placements;
        private readonly TerrainLayer[] _terrainLayers;
        private readonly TreeSurroundSpeciesTable _treeSurroundSpecies;
        private readonly TerrainVisualCache _visualCache;
        private readonly BiomeVisualSections _visualSections;
        private readonly WorldDataDirectory _worldCacheDirectory;

        // detailプロトタイプの並びはタイルに依らない。密度マップと同じ規則で一度だけ決める
        // The detail prototype order does not vary by tile and is decided once by the same rule as the density maps
        public IReadOnlyList<DetailPrototype> DetailPrototypes { get; }

        public TerrainTileVisualProvider(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
            SplatLayerTable layerTable, TerrainLayer[] terrainLayers, TreeSurroundSpeciesTable treeSurroundSpecies,
            IReadOnlyList<LedgerPlacement> placements, WorldDataDirectory worldCacheDirectory,
            TerrainVisualCache visualCache)
        {
            _config = config;
            _biomeTypes = biomeTypes;
            _visualSections = visualSections;
            _layerTable = layerTable;
            _terrainLayers = terrainLayers;
            _treeSurroundSpecies = treeSurroundSpecies;
            _placements = placements;
            _worldCacheDirectory = worldCacheDirectory;
            _visualCache = visualCache;

            // プロトタイプと密度マップは同じフラグで生死を共にする。片方だけ残すと本数が食い違ってSetDetailLayerへ届く前に落ちる
            // Prototypes and density maps live and die by one flag; keeping either alone breaks the counts before SetDetailLayer is ever reached
            DetailPrototypes = config.generateDetail
                ? TerrainDetailPrototypeList.Build(biomeTypes, visualSections)
                : new List<DetailPrototype>();
        }

        // CacheHitは呼び出し側の計測用。1枚ごとに再構築を省けたかを返す
        // CacheHit reports, tile by tile, whether the rebuild was skipped, for the caller's measurement
        public (TerrainTileVisual Visual, bool CacheHit) Resolve(
            int tileX, int tileZ, TerrainGenerationConfig tileConfig, Vector3 tileWorldPosition,
            float[,] preHeights, float[,] postHeights)
        {
            // splatもdetailも作らない設定では分類の結果を誰も読まない。パディング窓を回さずに空で返す
            // With neither splat nor detail requested nobody reads the classification, so the padded window is never run
            if (!_config.generateTexture && !_config.generateDetail)
                return (new TerrainTileVisual(null, Array.Empty<int[,]>()), false);

            // キャッシュ形式はalphamapを必ず1枚要求する。テクスチャを作らない見た目は書けないので読みも書きもしない
            // The cache format always demands one alphamap, so a texture-less visual is neither read nor written
            if (!_config.generateTexture) return (Rebuild(), false);

            // detailの解像度とプロトタイプ数を先に固定する。ヒット後に数違いで落とさず、Readerで壊れた取り逃しにする
            // Fix detail resolution and prototype count before loading so a mismatch becomes a broken miss in the Reader, never a post-hit failure
            var cacheHit = _visualCache.TryLoad(
                tileX, tileZ, _config.AlphamapResolution, _terrainLayers.Length, _config.Resolution - 1,
                DetailPrototypes.Count, out var tileVisual);
            if (cacheHit) return (tileVisual, true);

            // 取り逃したタイルだけをその場で作り直し、次回のために書き戻す
            // Only the missed tiles are rebuilt on the spot and written back for next time
            var rebuilt = Rebuild();
            _visualCache.Save(tileX, tileZ, rebuilt);
            return (rebuilt, false);

            #region Internal

            TerrainTileVisual Rebuild()
            {
                // 分類はタイル1枚につき1回。splatのブレンド入力とDetailの勝者マスクを同じパディング窓から採る
                // One classification per tile, so splat's blend inputs and detail's winner masks come from the same padded window
                using var classification = new TileClassificationContext(tileConfig, _biomeTypes);
                classification.Initialize();

                // 移植元はSplatmapJobそのものをフラグで飛ばす（TerrainGenerator.cs:792）。alphamapが無ければDetailのテクスチャフィルタも休む
                // The source gates the SplatmapJob itself (TerrainGenerator.cs:792); with no alphamap the detail texture filter idles too
                var alphamap = _config.generateTexture ? BuildAlphamap(classification) : null;
                var detailMaps = _config.generateDetail
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
                var transferredBiomeIndices = HeightFileLoader.LoadBiomeIndices(
                    _worldCacheDirectory, tileX, tileZ, _config.Resolution);

                return SplatmapStage.Generate(
                    tileConfig, _biomeTypes, classification, _layerTable, _visualSections, _treeSurroundSpecies,
                    preHeights, transferredBiomeIndices, _config.AlphamapResolution,
                    _placements, tileWorldPosition);
            }

            // 距離場はタイル境界の外まで見るため、切り出し済みのタイル内mapObjectではなく全タイルぶんを渡す
            // The distance fields look past the tile boundary, so the whole layout goes in rather than the tile's own slice
            List<int[,]> BuildDetailMaps(TileClassificationContext classification, float[,,] alphamap)
            {
                return TerrainDetailBuilder.Build(
                    tileConfig, _biomeTypes, _visualSections, preHeights, postHeights, classification.WinnerMasks,
                    alphamap, _terrainLayers, _placements, tileWorldPosition, tileX, tileZ);
            }

            #endregion
        }
    }
}

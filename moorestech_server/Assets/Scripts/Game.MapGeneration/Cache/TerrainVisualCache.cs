using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     表示用高さ・splatmap・detailの再構築結果をワールドのキャッシュ配下へ溜める。導出元が動けばキーが変わり、
    ///     取り逃した分だけ作り直して書き戻す（キャッシュが真実源になることは無い）
    ///     Accumulates rebuilt display heights, splatmaps and details under the world's cache; any moved input changes the key and
    ///     every miss is regenerated and written back, so the cache is never a source of truth
    /// </summary>
    public class TerrainVisualCache
    {
        private readonly string _cacheKey;
        private readonly WorldDataDirectory _worldCacheDirectory;

        public TerrainVisualCache(WorldDataDirectory worldCacheDirectory, string cacheKey)
        {
            _worldCacheDirectory = worldCacheDirectory;
            _cacheKey = cacheKey;
        }

        // 期待寸法を渡して食い違いを検出する。キーが一致するのに寸法が合わないファイルは中身が信用できない
        // The expected dimensions are passed in to catch disagreement: a matching key over mismatched dimensions is untrustworthy content
        public bool TryLoad(
            int tileX, int tileZ, int heightmapResolution, int alphamapResolution, int layerCount, int detailResolution,
            int detailMapCount, out TerrainTileVisual tileVisual)
        {
            var filePath = _worldCacheDirectory.TerrainVisualCacheFilePath(tileX, tileZ);
            var loaded = TerrainVisualCacheReader.TryRead(
                filePath, _cacheKey, heightmapResolution, alphamapResolution, layerCount, detailResolution, detailMapCount,
                out tileVisual, out var brokenReason);

            // 壊れたキャッシュは黙って使わず、黙って捨てもしない。取り逃しとして作り直したうえで痕跡を残す
            // A broken cache is neither used nor discarded in silence: it becomes a miss that regenerates, and leaves a trace
            if (brokenReason != null)
                Debug.LogWarning($"[TerrainVisualCache] Discarding '{filePath}': {brokenReason}.");

            return loaded;
        }

        public void Save(int tileX, int tileZ, TerrainTileVisual tileVisual)
        {
            var filePath = _worldCacheDirectory.TerrainVisualCacheFilePath(tileX, tileZ);

            // 書き手が保存先ディレクトリを用意する。それでも失敗する書き込みは隠さず呼び出し元へ返す
            // The writer provisions the destination directory; a write that still fails is surfaced instead of hidden
            TerrainVisualCacheWriter.Write(filePath, _cacheKey, tileVisual);
        }
    }
}

using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     splatmapとdetailの再構築結果をワールドのキャッシュ配下へ溜める。導出元が動けばキーが変わり、
    ///     取り逃した分だけ作り直して書き戻す（キャッシュが真実源になることは無い）
    ///     Accumulates rebuilt splatmaps and details under the world's cache; any moved input changes the key and
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
            int tileX, int tileZ, int alphamapResolution, int layerCount, int detailResolution, int detailMapCount,
            out TerrainTileVisual tileVisual)
        {
            var filePath = _worldCacheDirectory.TerrainVisualCacheFilePath(tileX, tileZ);
            var bootprofWatch = System.Diagnostics.Stopwatch.StartNew();
            var loaded = TerrainVisualCacheReader.TryRead(
                filePath, _cacheKey, alphamapResolution, layerCount, detailResolution, detailMapCount,
                out tileVisual, out var brokenReason);

            // 壊れたキャッシュは黙って使わず、黙って捨てもしない。取り逃しとして作り直したうえで痕跡を残す
            // A broken cache is neither used nor discarded in silence: it becomes a miss that regenerates, and leaves a trace
            if (brokenReason != null)
                Debug.LogWarning($"[TerrainVisualCache] Discarding '{filePath}': {brokenReason}.");

            Debug.Log($"[BOOTPROF] cache.tryLoad tile={tileX}_{tileZ} hit={loaded} ms={bootprofWatch.Elapsed.TotalMilliseconds:F1}");
            return loaded;
        }

        public void Save(int tileX, int tileZ, TerrainTileVisual tileVisual)
        {
            var filePath = _worldCacheDirectory.TerrainVisualCacheFilePath(tileX, tileZ);

            var bootprofSaveWatch = System.Diagnostics.Stopwatch.StartNew();
            // 書き手が保存先ディレクトリを用意する。それでも失敗する書き込みは隠さず呼び出し元へ返す
            // The writer provisions the destination directory; a write that still fails is surfaced instead of hidden
            TerrainVisualCacheWriter.Write(filePath, _cacheKey, tileVisual);
            Debug.Log($"[BOOTPROF] cache.save tile={tileX}_{tileZ} ms={bootprofSaveWatch.Elapsed.TotalMilliseconds:F1}");
        }
    }
}

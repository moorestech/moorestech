using System;
using System.IO;
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
            var loaded = TerrainVisualCacheReader.TryRead(
                filePath, _cacheKey, alphamapResolution, layerCount, detailResolution, detailMapCount,
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

            // 派生キャッシュへの外部I/O失敗だけを隔離し、再構築済みの見た目で起動を継続する
            // Isolate only external I/O failures for the derived cache so startup can continue with the rebuilt visuals
            try
            {
                TerrainVisualCacheWriter.Write(filePath, _cacheKey, tileVisual);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[TerrainVisualCache] Could not write '{filePath}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning($"[TerrainVisualCache] Access denied while writing '{filePath}': {exception.Message}");
            }
        }
    }
}

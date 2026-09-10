using System.IO;
using UnityEngine;

namespace Game.MapGeneration.Provisioning
{
    /// <summary>
    ///     共有キャッシュのうち現在のワールドIDと異なるものを消す。IDが内容由来になったので、別IDは別マスタか別seedの遺物であり再生成できる
    ///     Drops shared-cache worlds whose id differs from the current one; ids derive from content now, so another id is a relic of another master or seed and can be regenerated
    /// </summary>
    public static class StaleWorldCacheCollector
    {
        // キャッシュルートは呼び出し側が渡す。実ユーザーのキャッシュを消さずに検証できるようにするため
        // The caller supplies the cache root so this can be verified without wiping the real user cache
        public static void Collect(string cacheRoot, string currentWorldId)
        {
            if (!Directory.Exists(cacheRoot)) return;

            var removedCount = 0;
            foreach (var directory in Directory.GetDirectories(cacheRoot))
            {
                if (Path.GetFileName(directory) == currentWorldId) continue;
                Directory.Delete(directory, true);
                removedCount++;
            }

            if (0 < removedCount) Debug.Log($"[StaleWorldCacheCollector] Removed {removedCount} stale world cache(s) under '{cacheRoot}'.");
        }
    }
}

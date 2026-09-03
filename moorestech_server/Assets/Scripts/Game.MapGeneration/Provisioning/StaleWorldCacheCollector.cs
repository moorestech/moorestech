using System.IO;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Provisioning
{
    /// <summary>
    ///     共有キャッシュのうち現在のワールドIDと異なるものを消す。IDが内容由来になったので、別IDは別マスタか別seedの遺物であり再生成できる
    ///     Drops shared-cache worlds whose id differs from the current one; ids derive from content now, so another id is a relic of another master or seed and can be regenerated
    /// </summary>
    public static class StaleWorldCacheCollector
    {
        public static void Collect(string currentWorldId)
        {
            var cacheRoot = GameSystemPaths.WorldCacheDirectory;
            var removedCount = 0;
            foreach (var directory in Directory.GetDirectories(cacheRoot))
            {
                if (Path.GetFileName(directory) == currentWorldId) continue;
                Directory.Delete(directory, true);
                removedCount++;
            }

            if (removedCount > 0) Debug.Log($"[StaleWorldCacheCollector] Removed {removedCount} stale world cache(s) under '{cacheRoot}'.");
        }
    }
}

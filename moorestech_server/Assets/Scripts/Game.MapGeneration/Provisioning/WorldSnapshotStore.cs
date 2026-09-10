using System;
using System.IO;
using Game.MapGeneration.Export;
using Game.Paths;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.MapGeneration.Provisioning
{
    /// <summary>
    ///     生成済みワールドのスナップショット。共有キャッシュ(cache/worlds/worldId)をワールドと同じレイアウトで持ち、新規作成を生成でなくコピーで済ませる
    ///     復元源はビルド同梱(visual込み)→共有キャッシュの順。同梱から復元したときは共有キャッシュへも全量写し、クライアントの焼きがそこを読めるようにする
    ///     A pre-generated world snapshot: the shared cache (cache/worlds/worldId) keeps the world's own layout, so a new world is a copy rather than a generation
    ///     Sources are the bundled snapshot (visuals included) then the shared cache; a bundled restore also fills the shared cache so the client's bake reads it
    /// </summary>
    public static class WorldSnapshotStore
    {
        public static bool TryRestore(WorldDataDirectory worldDataDirectory, string serverDataDirectory, string worldId)
        {
            var sharedCache = WorldDataDirectory.ForWorldCache(worldId);
            var bundled = WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId);

            // 同梱源は見た目まで揃った完全体。共有キャッシュへ写してから、以後は共有キャッシュだけを源として扱う
            // The bundled source is complete down to the visuals; copy it into the shared cache, then treat the shared cache as the sole source
            // 本体だけの共有キャッシュにvisualを足せるのも同梱源だけなので、その欠損も取り込みの契機にする
            // Only the bundled source can supply visuals to a core-only shared cache, so a missing visual set also triggers the import
            var sharedCacheNeedsBundled = !IsSnapshot(sharedCache) || (!HasVisualCache(sharedCache) && HasVisualCache(bundled));
            if (IsSnapshot(bundled) && sharedCacheNeedsBundled)
            {
                // 1.2GBのコピー途中で落ちるとworld.jsonだけ揃った半端がスナップショット扱いになり恒久起動不能になる。一時先へ写してからリネームで確定する
                // A crash mid-way through the 1.2GB copy would leave a stub with world.json that passes as a snapshot and bricks boot; copy to a temp dir and commit by rename
                var cacheTemp = WorldDataDirectory.FromWorldRoot(sharedCache.ProvisioningTempDirectory);
                DiscardDirectory(cacheTemp.Root);
                CopyDirectory(bundled.Root, cacheTemp.Root);

                // ForWorldCacheが空のRootを作る副作用を持つため、リネーム先を空けてから確定する
                // ForWorldCache creates an empty root as a side effect, so clear the rename target before committing
                DiscardDirectory(sharedCache.Root);
                Directory.Move(cacheTemp.Root, sharedCache.Root);
                Debug.Log($"[WorldSnapshotStore] Copied bundled snapshot '{worldId}' into the shared cache.");
            }

            if (!IsSnapshot(sharedCache)) return false;

            // 本番Rootへの直書きはWorldProvisionerの「一時ディレクトリに書き切ってからDirectory.Moveで確定」の規約に揃える
            // Writing straight into the production root would bypass WorldProvisioner's rule of writing to a temp dir and committing via Directory.Move
            var worldTemp = WorldDataDirectory.FromWorldRoot(worldDataDirectory.ProvisioningTempDirectory);
            DiscardDirectory(worldTemp.Root);
            CopyWorldFiles(sharedCache, worldTemp);
            StampCreatedAt();
            Directory.Move(worldTemp.Root, worldDataDirectory.Root);
            Debug.Log($"[WorldSnapshotStore] Restored world '{worldId}' from the shared cache snapshot.");
            return true;

            #region Internal

            // 前回の中断で残った一時ディレクトリや空のリネーム先を捨てて再入を通す
            // Drops a temp dir left by an earlier interruption, or an empty rename target, so the retry can proceed
            void DiscardDirectory(string path)
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }

            // visualは本体と別に焼かれるので、本体だけ揃った共有キャッシュと完全体を区別する
            // Visuals are baked apart from the core, so this tells a core-only shared cache from a complete one
            bool HasVisualCache(WorldDataDirectory directory)
            {
                return Directory.Exists(directory.TerrainVisualDirectory) && 0 < Directory.GetFiles(directory.TerrainVisualDirectory).Length;
            }

            // createdAtは実世界の作成日時の記録。IDの導出には使わないので復元時刻で書き直す
            // createdAt records the real-world creation time; it no longer feeds the id, so it is rewritten to the restore time
            void StampCreatedAt()
            {
                var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldTemp.WorldMetaFilePath));
                worldMeta.CreatedAt = DateTime.UtcNow.ToString("O");
                File.WriteAllText(worldTemp.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));
            }

            #endregion
        }

        // 生成直後のワールド本体(world.json/map.json/terrain)を共有キャッシュへ写す。visualは先焼きが同じ場所へ既に書いている
        // Copies a freshly generated world's core (world.json/map.json/terrain) into the shared cache; the prebake already wrote the visuals there
        public static void Store(WorldDataDirectory worldDataDirectory, string worldId)
        {
            CopyWorldFiles(worldDataDirectory, WorldDataDirectory.ForWorldCache(worldId));
        }

        public static bool IsSnapshot(WorldDataDirectory directory)
        {
            return File.Exists(directory.WorldMetaFilePath) && File.Exists(directory.MapJsonFilePath) && Directory.Exists(directory.TerrainDirectory);
        }

        private static void CopyWorldFiles(WorldDataDirectory source, WorldDataDirectory destination)
        {
            Directory.CreateDirectory(destination.Root);
            File.Copy(source.MapJsonFilePath, destination.MapJsonFilePath, true);
            CopyDirectory(source.TerrainDirectory, destination.TerrainDirectory);

            // world.jsonはコミットマーカーなので最後に写す
            // world.json is the commit marker, so it is copied last
            File.Copy(source.WorldMetaFilePath, destination.WorldMetaFilePath, true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}

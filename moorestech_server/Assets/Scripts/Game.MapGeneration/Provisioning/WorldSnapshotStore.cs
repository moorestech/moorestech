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
            if (IsSnapshot(bundled) && !IsSnapshot(sharedCache))
            {
                CopyDirectory(bundled.Root, sharedCache.Root);
                Debug.Log($"[WorldSnapshotStore] Copied bundled snapshot '{worldId}' into the shared cache.");
            }

            if (!IsSnapshot(sharedCache)) return false;

            CopyWorldFiles(sharedCache, worldDataDirectory);
            StampCreatedAt(worldDataDirectory);
            Debug.Log($"[WorldSnapshotStore] Restored world '{worldId}' from the shared cache snapshot.");
            return true;
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

        // createdAtは実世界の作成日時の記録。IDの導出には使わないので復元時刻で書き直す
        // createdAt records the real-world creation time; it no longer feeds the id, so it is rewritten to the restore time
        private static void StampCreatedAt(WorldDataDirectory worldDataDirectory)
        {
            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            worldMeta.CreatedAt = DateTime.UtcNow.ToString("O");
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));
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

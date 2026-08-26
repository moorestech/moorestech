using System;
using System.IO;
using Core.Master;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mod.Config;
using Mod.Loader;
using Server.Boot;
using UnityEditor.Build;
using UnityEngine;

namespace Client.Editor.Build
{
    /// <summary>
    /// 既定seedの生成済みワールドを成果物へ同梱する。共有キャッシュに無ければ一時ワールドで生成し、キャッシュ(world本体+visual)を game/worldSnapshots/ へ写す
    /// Ships the default-seed pre-generated world with the artifact; generates it into a temp world when the shared cache lacks it, then copies the cache (world core + visuals) into game/worldSnapshots/
    /// </summary>
    public static class WorldSnapshotBundler
    {
        public static void Bundle(string outputDirectory, bool isStrict)
        {
            // 同梱済みのゲームデータを生成マスタの正本として使い、成果物と同じ指紋でIDを引く
            // Use the game data already bundled as the generation master's truth, so the id carries the artifact's own fingerprint
            var serverDataDirectory = Path.Combine(outputDirectory, "game");
            if (!Directory.Exists(serverDataDirectory))
            {
                if (isStrict) throw new BuildFailedException("[WorldSnapshotBundler] game data was not bundled; nothing to snapshot.");
                Debug.LogWarning("[WorldSnapshotBundler] game data was not bundled; skipping the world snapshot.");
                return;
            }

            var modResource = new ModsResource(Path.Combine(serverDataDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            var worldId = ProvisionIntoTemporaryWorld(serverDataDirectory);
            var sharedCache = WorldDataDirectory.ForWorldCache(worldId);
            var destination = WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId);
            if (Directory.Exists(destination.Root)) Directory.Delete(destination.Root, true);
            var copiedFileCount = DirectoryProcessor.CopyAndReplace(sharedCache.Root, destination.Root, Array.Empty<string>());
            Debug.Log($"[WorldSnapshotBundler] bundled world snapshot '{worldId}': {copiedFileCount} files at {destination.Root}");

            #region Internal

            // EnsureWorldはスナップショット命中ならコピー、ミスなら生成+先焼き+共有キャッシュへの書き戻しまで行う。一時ワールドは捨てる
            // EnsureWorld copies on a snapshot hit, or generates, prebakes and writes back to the shared cache on a miss; the temp world itself is discarded
            static string ProvisionIntoTemporaryWorld(string serverDataDirectory)
            {
                var temporaryRoot = Path.Combine(GameSystemPaths.TmpFileDirectory, "world-snapshot-bundle");
                if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
                var worldDataDirectory = WorldDataDirectory.FromWorldRoot(temporaryRoot);
                WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                    worldDataDirectory, serverDataDirectory, WorldMapMode.Generated, ServerInstanceManager.DefaultGeneratedSeed));
                var worldId = TerrainTransferMetaReader.Read(worldDataDirectory).WorldId;
                Directory.Delete(temporaryRoot, true);
                return worldId;
            }

            #endregion
        }
    }
}

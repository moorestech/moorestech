#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Client.Common;
using Core.Master;
using Game.Map.Interface.Json;
using Game.MapGeneration.Facade;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json;
using Server.Boot;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;

namespace Client.MapScene.Editor
{
    public sealed class GeneratedMapPreviewWorld : IDisposable
    {
        private readonly WorldDataDirectory _files;
        public WorldTerrainSession TerrainSession { get; private set; }
        public MapInfoJson Map { get; private set; }

        private GeneratedMapPreviewWorld(WorldDataDirectory files)
        {
            _files = files;
        }

        public static GeneratedMapPreviewWorld Create(string serverDataDirectory)
        {
            using var process = Process.GetCurrentProcess();
            var root = Path.Combine(TemporaryDirectory, process.Id.ToString(), Guid.NewGuid().ToString("N"));
            var world = new GeneratedMapPreviewWorld(WorldDataDirectory.FromWorldRoot(root));
            try
            {
                // Importerと同順で外部JSONを読み、未作成rootを既存生成器へ渡す
                // Load external JSON in importer order and pass a nonexistent root to the existing provisioner
                var resources = new ModsResource(ServerConst.CreateServerModsDirectory(serverDataDirectory));
                MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resources)));
                DefaultGeneratedWorldProvisioner.EnsureWorld(world._files, serverDataDirectory);
                world.Map = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(world._files.MapJsonFilePath));
                world.TerrainSession = WorldTerrainSession.Open(TerrainTransferMetaReader.Read(world._files), serverDataDirectory);
                return world;
            }
            catch (Exception exception)
            {
                // ディスクとJSONの外部境界。所有権を返す前の失敗も生成物を回収して伝播する
                // Disk and JSON are external boundaries; reclaim allocations before ownership transfer and propagate failure
                Debug.LogError($"[GeneratedMapPreview] World creation failed; preview unavailable. Root:{root}\n{exception}");
                world.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // 共有キャッシュ・snapshotには触れず、この走行の確定先と作業先だけを消す
            // Leave shared caches and snapshots intact, deleting only this run's destination and staging directory
            try { DeleteDirectory(_files.Root); }
            finally { DeleteDirectory(_files.ProvisioningTempDirectory); }
        }

        private static string TemporaryDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "GeneratedMapPreview");

        [InitializeOnLoadMethod]
        private static void ReclaimAbandonedProcesses()
        {
            if (!Directory.Exists(TemporaryDirectory)) return;
            try
            {
                // 起動・reload時に生存PIDを保護し、終了済みEditorの専用領域だけを回収する
                // Protect live process IDs at startup and reload, reclaiming only departed editors' dedicated directories
                var activeProcesses = new HashSet<int>();
                foreach (var process in Process.GetProcesses())
                {
                    using (process) activeProcesses.Add(process.Id);
                }
                foreach (var directory in Directory.GetDirectories(TemporaryDirectory))
                    if (int.TryParse(Path.GetFileName(directory), out var processId) && !activeProcesses.Contains(processId))
                        DeleteDirectory(directory);
            }
            catch (Exception exception)
            {
                // OSプロセス列挙・ディスクIO境界。失敗した回収領域を明示して起動は継続する
                // OS process enumeration and disk IO are boundaries; disclose retained temporary data while allowing startup
                Debug.LogError($"[GeneratedMapPreview] Abandoned temporary data could not be fully reclaimed: {TemporaryDirectory}\n{exception}");
            }
        }

        private static void DeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (Exception exception)
            {
                // 削除も外部ディスク境界。残った専用領域をログし、成功扱いにしない
                // Deletion also crosses the disk boundary; log the retained directory and do not report success
                Debug.LogError($"[GeneratedMapPreview] Temporary data remains at {directory}\n{exception}");
                throw;
            }
        }
    }
}
#endif

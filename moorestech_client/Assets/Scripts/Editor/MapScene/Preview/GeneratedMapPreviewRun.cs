#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using Server.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.MapScene.Editor
{
    public sealed class GeneratedMapPreviewRun : IDisposable
    {
        private const int EditorYieldPlacementInterval = 50;
        private GeneratedMapPreviewWorld _world;
        private GeneratedMapPreviewContent _content;
        private bool _executed;
        private bool _disposed;
        internal int ExpectedMapObjectCount { get; private set; }
        internal int CreatedMapObjectCount { get; private set; }
        internal int ExpectedOutcropCount { get; private set; }
        internal int CreatedOutcropCount { get; private set; }
        internal int MissingPlacementCount => ExpectedMapObjectCount - CreatedMapObjectCount + ExpectedOutcropCount - CreatedOutcropCount;
        internal Vector3 SpawnPosition { get; private set; }
        internal Bounds TerrainBounds { get; private set; }

        public async UniTask ExecuteAsync(Scene scene, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GeneratedMapPreviewRun));
            if (_executed) throw new InvalidOperationException("A preview run can only execute once.");
            _executed = true;
            cancellationToken.ThrowIfCancellationRequested();

            // UIへ制御を返し、reload直前のCancelも次の更新を待たず終端へ伝える
            // Return control to the UI and propagate cancellation before reload without waiting for another update
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken, true);
            cancellationToken.ThrowIfCancellationRequested();
            _world = GeneratedMapPreviewWorld.Create(ServerDirectory.GetDirectory());
            ExpectedMapObjectCount = _world.Map.MapObjects.Count;
            _content = new GeneratedMapPreviewContent(scene);
            var assets = new EditorTerrainAssetLoader();
            await GeneratedMapPreviewTerrain.BuildAsync((TiledTerrainSession)_world.TerrainSession, _content, assets, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // 全地形Boundsとスポーン位置を渡す
            // Pass full terrain bounds and authoritative spawn position for framing
            var terrains = _content.Root.GetComponentsInChildren<Terrain>();
            if (terrains.Length == 0)
            {
                var message = $"[GeneratedMapPreview] No terrain tiles were built. ExpectedTiles:{_world.TerrainSession.Layout.TileCoordinates.Count}";
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }
            var bounds = new Bounds(terrains[0].transform.position + terrains[0].terrainData.bounds.center, terrains[0].terrainData.bounds.size);
            foreach (var terrain in terrains)
                bounds.Encapsulate(new Bounds(terrain.transform.position + terrain.terrainData.bounds.center, terrain.terrainData.bounds.size));
            TerrainBounds = bounds;
            SpawnPosition = _world.Map.DefaultSpawnPointJson.Position;
            var spawn = CreateChild("SpawnPoint");
            spawn.transform.position = SpawnPosition;
            spawn.AddComponent<SpawnPointObject>();

            // 照明は専用Sceneの子として所有し、主SceneのRenderSettingsは変更しない
            // Own lighting as a child in the dedicated scene without changing main-scene RenderSettings
            var lightObject = CreateChild("PreviewLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            await PlaceMapObjectsAsync(assets, CreateChild("MapObjects").transform, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await PlaceOutcropsAsync(assets, CreateChild("VeinOutcrops").transform, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            #region Internal

            GameObject CreateChild(string name)
            {
                var child = new GameObject(name);
                child.transform.SetParent(_content.Root, false);
                return child;
            }

            #endregion
        }

        internal void RetainPendingContent() => _content?.RetainUntilDisposed();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _content?.Dispose(); }
            finally { _world?.Dispose(); }
        }

        private async UniTask PlaceOutcropsAsync(EditorTerrainAssetLoader assets, Transform parent, CancellationToken cancellationToken)
        {
            // 公開された鉱脈配置から露頭を作り、欠損を走行全体へ集計する
            // Render outcrops from public vein placements and include missing instances in the run's accounting
            ExpectedOutcropCount = _world.Map.MapVeins.Count;
            CreatedOutcropCount = await GeneratedMapPreviewOutcrops.PlaceAsync(_world.Map.MapVeins, assets, parent, cancellationToken);
        }

        private async UniTask PlaceMapObjectsAsync(EditorTerrainAssetLoader assets, Transform parent, CancellationToken cancellationToken)
        {
            var prefabs = new Dictionary<Guid, GameObject>();
            for (var index = 0; index < ExpectedMapObjectCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = _world.Map.MapObjects[index];
                var prefab = await ResolvePrefabAsync(info.MapObjectGuid);
                cancellationToken.ThrowIfCancellationRequested();
                if (prefab != null && MapObjectPrefabPlacement.Instantiate(info, prefab, parent) != null)
                    CreatedMapObjectCount++;

                // 配置値を保ち、一定数ごとに制御を返す
                // Preserve placement values and yield at the editor interval
                if ((index + 1) % EditorYieldPlacementInterval != 0) continue;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken, true);
                cancellationToken.ThrowIfCancellationRequested();
            }

            #region Internal

            async UniTask<GameObject> ResolvePrefabAsync(Guid guid)
            {
                if (prefabs.TryGetValue(guid, out var cached)) return cached;
                var element = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(guid);
                if (element == null)
                {
                    Debug.LogError($"[GeneratedMapPreview] Missing map object master. MapObjectGuid:{guid}");
                    prefabs.Add(guid, null);
                    return null;
                }

                GameObject prefab;
                try
                {
                    prefab = await assets.LoadAsync<GameObject>(element.AddressablePath, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // AssetDatabaseは外部資産境界。失敗もGUID単位で記憶し、全配置分を欠損へ集計する
                    // AssetDatabase is an external asset boundary; cache failure per GUID and count every affected placement as missing
                    Debug.LogError($"[GeneratedMapPreview] Prefab unavailable. MapObjectGuid:{guid} Address:{element.AddressablePath}\n{exception}");
                    prefab = null;
                }
                prefabs.Add(guid, prefab);
                return prefab;
            }

            #endregion
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.MapScene.Editor
{
    public sealed class GeneratedMapPreviewContent : IDisposable
    {
        private readonly List<TerrainData> _terrainData = new();
        private bool _disposed;
        public Transform Root { get; }

        public GeneratedMapPreviewContent(Scene scene)
        {
            // 子を作る前に専用Sceneへ移し、所有範囲をこのrootへ閉じる
            // Move into the dedicated scene before adding children, keeping ownership under this root
            var root = new GameObject("GeneratedMapPreview");
            SceneManager.MoveGameObjectToScene(root, scene);
            Root = root.transform;
        }

        public TerrainData CreateTerrainData()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GeneratedMapPreviewContent));
            var data = new TerrainData();
            _terrainData.Add(data);
            return data;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Terrainの参照を先に外し、自分が確保したnative dataだけを解放する
            // Remove terrain references first, then release only native data allocated here
            if (Root != null) Object.DestroyImmediate(Root.gameObject);
            foreach (var data in _terrainData) Object.DestroyImmediate(data);
            _terrainData.Clear();
        }
    }
}
#endif

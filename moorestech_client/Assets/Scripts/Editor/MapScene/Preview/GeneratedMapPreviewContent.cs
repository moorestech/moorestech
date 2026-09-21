#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.MapScene.Editor
{
    public sealed class GeneratedMapPreviewContent : IDisposable
    {
        private readonly List<TerrainData> _terrainData = new();
        private Scene _retainedScene;
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
            // 組立待ちでTerrain未接続でも、Stage遷移時の未使用アセット回収に所有物を渡さない
            // Keep owned data alive across stage-switch asset collection even before assembly attaches it to a terrain
            var data = new TerrainData { hideFlags = HideFlags.DontUnloadUnusedAsset };
            _terrainData.Add(data);
            return data;
        }

        internal void RetainUntilDisposed()
        {
            // StageのSceneは同期で閉じるため、未完了処理のrootを専用Sceneで保つ
            // The stage scene closes synchronously, so retain the pending operation's root in an owned scene
            _retainedScene = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(Root.gameObject, _retainedScene);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Terrainの参照を先に外し、自分が確保したnative dataだけを解放する
            // Remove terrain references first, then release only native data allocated here
            try
            {
                if (Root != null) Object.DestroyImmediate(Root.gameObject);
                foreach (var data in _terrainData) Object.DestroyImmediate(data);
                _terrainData.Clear();
            }
            finally
            {
                if (_retainedScene.IsValid()) EditorSceneManager.ClosePreviewScene(_retainedScene);
            }
        }
    }
}
#endif

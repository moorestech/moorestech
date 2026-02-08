using Client.Common.Asset;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.DebugSystem
{
    public static class DebugObjectsBootstrap
    {
        private const string DebugObjectsAddress = "Vanilla/Debug/DebugObjects";
        
        private static GameObject _debugObjectsInstance;
        private static bool _isCreatingDebugObjects;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // シーンロード時にイベント購読を初期化する
            // Initialize scene-loaded subscription.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private static async void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            // 生成済み/生成中/ビルド対象外のシーンでは何もしない
            // Skip when already created, creating, or non-build scene.
            if (_debugObjectsInstance != null || _isCreatingDebugObjects || !IsBuildTargetScene(scene.name)) return;
            
            _isCreatingDebugObjects = true;
            var loadedAsset = await AddressableLoader.LoadAsync<GameObject>(DebugObjectsAddress);
            if (loadedAsset == null)
            {
                _isCreatingDebugObjects = false;
                return;
            }
            
            // DebugObjectsを永続ルートに移動する
            // Move DebugObjects to persistent root.
            _debugObjectsInstance = Object.Instantiate(loadedAsset.Asset);
            loadedAsset.Dispose();
            Object.DontDestroyOnLoad(_debugObjectsInstance);
            
            _isCreatingDebugObjects = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        
        private static bool IsBuildTargetScene(string sceneName)
        {
            // ビルド設定に含まれるシーン名と一致するかを確認する
            // Check whether scene name is included in build settings.
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var buildScenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var buildSceneName = System.IO.Path.GetFileNameWithoutExtension(buildScenePath);
                if (buildSceneName == sceneName) return true;
            }
            
            return false;
        }
    }
}

using Client.Common.Asset;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Client.DebugSystem
{
    public static class DebugObjectsBootstrap
    {
        private const string DebugObjectsAddress = "Vanilla/Debug/DebugObjects";
        
        private static GameObject _debugObjectsInstance;
        private static LoadedAsset<GameObject> _debugObjectsAsset;
        private static bool _isCreatingDebugObjects;
        
        // テスト時にデバッグオブジェクトの生成を無効化するフラグ
        // Flag to disable debug object creation during tests.
        public static bool Disabled { get; set; }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (Disabled) return;

#if UNITY_EDITOR
            // SessionStateはドメインリロード後も保持されるため、テスト中の無効化に使用
            // SessionState persists across domain reload, used to disable during tests.
            if (SessionState.GetBool("DebugObjectsBootstrap_Disabled", false)) return;
#endif
            
            // シーンロード時にイベント購読を初期化する
            // Initialize scene-loaded subscription.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // アプリ終了時にAddressables参照を解放する
            // Release Addressables reference on app quit.
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
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
            
            _debugObjectsAsset = loadedAsset;
            
            // 非アクティブ状態でインスタンスを作成し、DontDestroyOnLoad後にアクティブ化する
            // 子コンポーネントのAwakeがDontDestroyOnLoad前に走ると警告やNullRefが発生するため
            // Instantiate inactive, apply DontDestroyOnLoad, then activate.
            // Child component Awake running before DontDestroyOnLoad causes warnings and NullRef.
            var wasActive = loadedAsset.Asset.activeSelf;
            loadedAsset.Asset.SetActive(false);
            _debugObjectsInstance = Object.Instantiate(loadedAsset.Asset);
            loadedAsset.Asset.SetActive(wasActive);
            
            Object.DontDestroyOnLoad(_debugObjectsInstance);
            _debugObjectsInstance.SetActive(true);
            
            ActivateDebugLogPopup(_debugObjectsInstance);
            
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
        
        private static void ActivateDebugLogPopup(GameObject debugObjectsInstance)
        {
            // 非アクティブなPopupを有効化して初期化を保証する
            // Ensure inactive popup gets initialized by activating it.
            var popupTransform = debugObjectsInstance.transform.Find("IngameDebugConsole/DebugLogPopup");
            if (popupTransform == null || popupTransform.gameObject.activeSelf) return;
            popupTransform.gameObject.SetActive(true);
        }
        
        private static void OnApplicationQuitting()
        {
            // 保持したAddressables参照を明示解放する
            // Explicitly release held Addressables reference.
            if (_debugObjectsAsset == null) return;
            _debugObjectsAsset.Dispose();
            _debugObjectsAsset = null;
        }
    }
}

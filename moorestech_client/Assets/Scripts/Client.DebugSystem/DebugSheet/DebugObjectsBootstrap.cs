using Client.Common.Asset;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.DebugSystem
{
    public static class DebugObjectsBootstrap
    {
        private const string DebugObjectsAddress = "Vanilla/Debug/DebugObjects";
        private const string DisabledSessionStateKey = "DebugObjectsBootstrap_Disabled";
        
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
            if (ConsumeDisableForNextInitialize()) return;
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

#if UNITY_EDITOR
        private static void ClearDisableForNextInitialize()
        {
            // テスト後の明示クリーンアップで通常PlayModeへの漏れを防ぐ
            // Prevent test cleanup state from leaking into normal PlayMode.
            UnityEditor.SessionState.SetBool(DisabledSessionStateKey, false);
        }

        private static bool ConsumeDisableForNextInitialize()
        {
            if (!UnityEditor.SessionState.GetBool(DisabledSessionStateKey, false)) return false;

            // テスト中断後に通常PlayModeへ漏れないよう一度だけ消費する
            // Consume once so aborted tests do not suppress normal PlayMode.
            UnityEditor.SessionState.SetBool(DisabledSessionStateKey, false);
            return true;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditorDisableFlagCleanup()
        {
            // EditMode中の再コンパイルで残留フラグを消す
            // Clear stale flags on script reload while the editor stays in EditMode.
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) ClearDisableForNextInitialize();

            // PlayMode終了時にも残留フラグを消す
            // Clear stale flags when PlayMode returns to EditMode.
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
        }

        private static void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
            ClearDisableForNextInitialize();
        }
#endif
    }
}

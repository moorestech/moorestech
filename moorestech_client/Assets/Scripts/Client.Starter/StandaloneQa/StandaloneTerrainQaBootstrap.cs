using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Client.Common;
using Client.DebugSystem.Environment;
using Client.Game.Common;
using Common.Debug;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Client.Starter.StandaloneQa
{
    public static class StandaloneTerrainQaBootstrap
    {
        private const string ResultFileName = "result.json";
        private const string ScreenshotFileName = "generated-terrain-player.png";
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private static readonly Stopwatch LoadingStopwatch = new();

        private static StandaloneTerrainQaSettings _settings;
        private static bool _isActive;
        private static bool _hasInjectedSettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (Application.isEditor) return;

            var args = System.Environment.GetCommandLineArgs();
            if (!StandaloneTerrainQaSettings.HasMarker(args)) return;
            if (!StandaloneTerrainQaSettings.TryParse(args, out _settings, out var error))
            {
                Debug.LogError($"[StandaloneTerrainQa] {error}");
                Application.Quit(2);
                return;
            }

            // Player QA専用cacheへ環境設定を隔離し、初期化完了時の再適用もRuntimeへ固定する
            // Isolate environment settings in a Player-QA cache and keep post-initialization reapplication on Runtime
            var debugCacheDirectory = Path.Combine(_settings.ResultDirectory, "debug-cache");
            Directory.CreateDirectory(debugCacheDirectory);
            DebugParametersCacheDirectory.SetOverride(debugCacheDirectory);
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Runtime);

            // 専用引数が揃ったPlayerだけでシーン注入と完了検査を有効にする
            // Enable scene injection and completion checks only for a Player with complete dedicated arguments
            _isActive = true;
            LoadingStopwatch.Restart();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameInitializedEvent.OnGameInitialized.Take(1).Subscribe(_ => ValidateAndExitAsync().Forget());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_isActive) return;

            if (scene.name == SceneConstant.GameInitializerSceneName)
            {
                var pipeline = UnityEngine.Object.FindFirstObjectByType<InitializeScenePipeline>(FindObjectsInactive.Include);
                if (pipeline == null)
                {
                    ExitWithFailure("InitializeScenePipeline was not found");
                    return;
                }

                // sceneLoadedはStartより先なので、通常初期化が読む正式プロパティをここで差し替える
                // sceneLoaded runs before Start, so replace the official properties read by normal initialization here
                pipeline.SetProperty(_settings.CreateInitializeProprieties());
                _hasInjectedSettings = true;
                return;
            }

            if (scene.name == SceneConstant.MainGameSceneName)
            {
                if (!_hasInjectedSettings || !DebugEnvironmentController.TrySetEnvironment(DebugEnvironmentType.Runtime))
                {
                    ExitWithFailure("runtime environment could not be selected before MainGame initialization");
                }
                return;
            }

            // 初回シーンはビルド設定に依存するため、QA起動時だけ初期化シーンへ正規化する
            // The first scene depends on build settings, so normalize it to the initializer only during QA boot
            if (!_hasInjectedSettings)
            {
                SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
                return;
            }

            ExitWithFailure($"unexpected scene loaded after initialization started: {scene.name}");
        }

        private static async UniTask ValidateAndExitAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3), DelayType.Realtime);
            Directory.CreateDirectory(_settings.ResultDirectory);

            // Player実体が生成した全Terrainのマテリアルとshader対応状態を検査する
            // Inspect material and shader support on every Terrain created by the actual Player
            var terrains = Terrain.activeTerrains;
            var invalidTerrains = terrains.Where(IsInvalidTerrain).Select(terrain => terrain.name).ToArray();
            var shaderNames = terrains.Select(GetShaderName).Distinct().OrderBy(name => name).ToArray();
            var screenshotPath = Path.Combine(_settings.ResultDirectory, ScreenshotFileName);
            await CaptureScreenshotAsync(screenshotPath);

            var screenshotExists = File.Exists(screenshotPath);
            var success = 0 < terrains.Length && invalidTerrains.Length == 0 && screenshotExists;
            var result = new StandaloneTerrainQaResult
            {
                success = success,
                terrainCount = terrains.Length,
                invalidTerrainNames = invalidTerrains,
                shaderNames = shaderNames,
                screenshotPath = screenshotPath,
                elapsedMilliseconds = LoadingStopwatch.ElapsedMilliseconds,
                message = success
                    ? "all runtime terrain shaders are supported"
                    : $"runtime terrain validation failed; screenshotExists={screenshotExists}",
            };

            WriteResult(result);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.Quit(success ? 0 : 1);

            #region Internal

            static bool IsInvalidTerrain(Terrain terrain)
            {
                return terrain.materialTemplate == null ||
                       terrain.materialTemplate.shader == null ||
                       !terrain.materialTemplate.shader.isSupported;
            }

            static string GetShaderName(Terrain terrain)
            {
                return terrain.materialTemplate == null || terrain.materialTemplate.shader == null
                    ? "<missing>"
                    : terrain.materialTemplate.shader.name;
            }

            #endregion
        }

        private static async UniTask CaptureScreenshotAsync(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);

            // ScreenCaptureの非同期書き出し完了を期限付きで待つ
            // Wait with a deadline for the asynchronous ScreenCapture write
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }
        }

        private static void ExitWithFailure(string message)
        {
            Debug.LogError($"[StandaloneTerrainQa] {message}");
            if (_settings != null)
            {
                Directory.CreateDirectory(_settings.ResultDirectory);
                WriteResult(new StandaloneTerrainQaResult { success = false, message = message });
            }
            Application.Quit(1);
        }

        private static void WriteResult(StandaloneTerrainQaResult result)
        {
            var path = Path.Combine(_settings.ResultDirectory, ResultFileName);
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
        }

        [Serializable]
        private sealed class StandaloneTerrainQaResult
        {
            public bool success;
            public int terrainCount;
            public string[] invalidTerrainNames;
            public string[] shaderNames;
            public string screenshotPath;
            public long elapsedMilliseconds;
            public string message;
        }
    }
}

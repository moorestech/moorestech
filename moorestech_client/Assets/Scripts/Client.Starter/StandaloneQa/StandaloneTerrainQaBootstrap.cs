using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Client.Common;
using Client.DebugSystem.Environment;
using Client.Game;
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
        private const string ScreenshotFileName = "generated-terrain-player.png";
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private static readonly Stopwatch LoadingStopwatch = new();
        private static StandaloneTerrainQaSettings _settings;
        private static bool _isActive;
        private static bool _hasInjectedSettings;
        private static bool _validationStarted;
        private static bool _gameInitialized;
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
            // Player QA専用cacheへgenerated用環境とSkit抑止を隔離し、Terrainを表示したまま検証する
            // Isolate generated environment and skit suppression in the Player-QA cache, keeping Terrain visible for validation
            var debugCacheDirectory = Path.Combine(_settings.ResultDirectory, "debug-cache");
            Directory.CreateDirectory(debugCacheDirectory);
            DebugParametersCacheDirectory.SetOverride(debugCacheDirectory);
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.PureNature);
            DebugParameters.SaveBool(DebugConst.SkitPlaySettingsKey, true);

            // 専用引数が揃ったPlayerだけでシーン注入と完了検査を有効にする
            // Enable scene injection and completion checks only for a Player with complete dedicated arguments
            _isActive = true;
            LoadingStopwatch.Restart();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameInitializedEvent.OnGameInitialized.Take(1).Subscribe(_ => _gameInitialized = true);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_isActive) return;
            Debug.Log($"[StandaloneTerrainQa] sceneLoaded={scene.name}");
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
                if (!_hasInjectedSettings || !DebugEnvironmentController.TrySetEnvironment(DebugEnvironmentType.PureNature))
                {
                    ExitWithFailure("runtime environment could not be selected before MainGame initialization");
                    return;
                }

                if (_validationStarted) return;
                _validationStarted = true;
                ValidateAndExitAsync().Forget();
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
            // 地形構築後に発火する初期化完了を先に待ち、差し替え前のオーサリングTerrainを成功条件にしない
            // Wait first for initialization fired after terrain construction, excluding authored Terrain before replacement from success
            var initializationDeadline = Time.realtimeSinceStartup + 120f;
            while (!_gameInitialized && Time.realtimeSinceStartup < initializationDeadline)
            {
                await UniTask.Yield();
            }
            if (!_gameInitialized)
            {
                ExitWithFailure("game initialization did not complete within 120 seconds");
                return;
            }

            // 初期化完了後に有効なTerrainを待ち、Player固有のHierarchy反映遅延にも期限を設ける
            // Wait for active Terrain after initialization, bounding Player-specific hierarchy propagation delays
            var terrainDeadline = Time.realtimeSinceStartup + 30f;
            while (StandaloneTerrainQaViewPreparer.GetGeneratedTerrains().Length == 0 &&
                   Time.realtimeSinceStartup < terrainDeadline)
            {
                await UniTask.Yield();
            }
            if (StandaloneTerrainQaViewPreparer.GetGeneratedTerrains().Length == 0)
            {
                ExitWithFailure("runtime terrain did not become active within 30 seconds after initialization");
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(3), DelayType.Realtime);
            Directory.CreateDirectory(_settings.ResultDirectory);

            // Player実体が生成した全Terrainのマテリアルとshader対応状態を検査する
            // Inspect material and shader support on every Terrain created by the actual Player
            var terrains = StandaloneTerrainQaViewPreparer.GetGeneratedTerrains();
            var invalidTerrains = terrains.Where(IsInvalidTerrain).Select(terrain => terrain.name).ToArray();
            var shaderNames = terrains.Select(GetShaderName).Distinct().OrderBy(name => name).ToArray();
            var screenshotPath = Path.Combine(_settings.ResultDirectory, ScreenshotFileName);
            // 初期化完了後の撮影だけauthored環境を隠し、runtime Terrainの原点と見た目を単独で証跡化する
            // Hide authored environments only for the post-initialization capture, isolating runtime Terrain origin and visuals
            if (!DebugEnvironmentController.TrySetEnvironment(DebugEnvironmentType.Runtime))
            {
                ExitWithFailure("authored environments could not be hidden for runtime terrain capture");
                return;
            }
            StandaloneTerrainQaViewPreparer.Prepare(terrains[0]);
            await UniTask.Delay(TimeSpan.FromSeconds(1), DelayType.Realtime);
            await StandaloneTerrainQaEvidenceWriter.CaptureScreenshotAsync(screenshotPath);

            var screenshotExists = File.Exists(screenshotPath);
            var success = 0 < terrains.Length && invalidTerrains.Length == 0 && screenshotExists && _gameInitialized;
            var result = new StandaloneTerrainQaResult
            {
                success = success,
                gameInitialized = _gameInitialized,
                terrainCount = terrains.Length,
                invalidTerrainNames = invalidTerrains,
                shaderNames = shaderNames,
                screenshotPath = screenshotPath,
                elapsedMilliseconds = LoadingStopwatch.ElapsedMilliseconds,
                message = success
                    ? "all runtime terrain shaders are supported"
                    : $"runtime terrain validation failed; screenshotExists={screenshotExists}; gameInitialized={_gameInitialized}",
            };

            StandaloneTerrainQaEvidenceWriter.WriteResult(_settings.ResultDirectory, result);
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

        private static void ExitWithFailure(string message)
        {
            Debug.LogError($"[StandaloneTerrainQa] {message}");
            if (_settings != null)
            {
                Directory.CreateDirectory(_settings.ResultDirectory);
                StandaloneTerrainQaEvidenceWriter.WriteResult(
                    _settings.ResultDirectory, new StandaloneTerrainQaResult { success = false, message = message });
            }
            Application.Quit(1);
        }
    }
}

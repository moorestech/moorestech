using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Modal;
using Client.Network.API;
using Client.Network.Settings;
using Client.Starter.Initialization;
using Cysharp.Threading.Tasks;
using Game.Context;
using Server.Boot;
using Server.Boot.Args;
using Server.Util.MessagePack;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Debug = UnityEngine.Debug;

namespace Client.Starter
{
    /// <summary>
    ///     シーンのロード、アセットのロード、サーバーとの接続を行う
    ///     TODO 何かが失敗したらそのログを出すようにする
    /// </summary>
    public class InitializeScenePipeline : MonoBehaviour
    {
        // ツールバーの「セーブをロード・保存しない再生ボタン」が立てるセッションフラグのキー
        // SessionState key set by the toolbar "play without loading/saving" button
        public const string SkipSaveLoadSessionKey = "moorestech_SkipSaveLoadPlayMode";

        [SerializeField] private BlockIconImagePhotographer blockIconImagePhotographer;
        [SerializeField] private BlockGameObject missingBlockIdObject;

        [SerializeField] private TMP_Text loadingLog;
        [SerializeField] private Button backToMainMenuButton;

        private InitializeProprieties _proprieties = InitializeProprieties.CreateDefault();
        public void SetProperty(InitializeProprieties proprieties)
        {
            _proprieties = proprieties;
        }

        private void Awake()
        {
            backToMainMenuButton.onClick.AddListener(() => SceneManager.LoadScene(SceneConstant.MainMenuSceneName));
        }

        private void Start()
        {
            Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            // ---- Web UI サーバーの起動（最序盤）----
            // GameShutdownEvent の購読は WebUiHost 側で 1 度だけ張られる
            // ---- Web UI server bootstrap (earliest phase) ----
            // The GameShutdownEvent subscription is installed once inside WebUiHost itself
            //
            // WebUI は非必須。起動失敗（ポート衝突・node 欠如による Win32Exception 等）でゲーム本体を止めないよう全例外を隔離する（2-A）
            // WebUI is non-essential; isolate ALL startup failures (port collision, missing node → Win32Exception, etc.) so game boot is never blocked (2-A)
            try
            {
                // 起動成否を uGUI/Web 表示ゲートへ伝える。失敗（例外/false）なら uGUI を出すため無効化する
                // Propagate startup success to the uGUI/Web gate; on failure (exception/false) disable it so uGUI shows
                var hostStarted = await Client.WebUiHost.Boot.WebUiHost.StartAsync();
                Client.Game.InGame.UI.UIState.WebUiScreenGate.SetHostAvailable(hostStarted);
            }
            catch (Exception e)
            {
                // WebUI 無しでゲーム続行。外部プロセス境界の起動失敗を隔離して再試行可能にする
                // Continue without WebUI; isolate external-process startup failures and keep retries possible
                Client.Game.InGame.UI.UIState.WebUiScreenGate.SetHostAvailable(false);
                Debug.LogWarning($"[WebUiHost] start skipped: {e.Message}");
            }

#if UNITY_EDITOR
            // ツールバーの専用再生ボタン経由なら、セーブデータをロード・保存しないよう起動引数を上書きする
            // When launched via the dedicated toolbar play button, override launch args to skip loading/saving save data
            ApplySkipSaveLoadModeIfNeeded(_proprieties);
#endif

            var args = CliConvert.Parse<StartServerSettings>(_proprieties.CreateLocalServerArgs);
            var serverDirectory = args.ServerDataDirectory;

            var loadingStopwatch = new Stopwatch();
            loadingStopwatch.Start();

            // Addressablesを初期化し、並列ロードでハングするアセットを先に読む
            // Initialize Addressables and pre-load assets that hang during parallel loading
            var initializeHandle = Addressables.InitializeAsync();
            await initializeHandle.ToUniTask();
            await ModAssetLoader.PreloadCriticalAssetsAsync();

            _proprieties ??= InitializeProprieties.CreateDefault();

            // DIコンテナによるServerContextの作成
            if (!ServerContext.IsInitialized)
            {
                var options = new MoorestechServerDIContainerOptions(serverDirectory);
                new MoorestechServerDIContainerGenerator().Create(options);
            }

            var playerConnectionSetting = new PlayerConnectionSetting(_proprieties.PlayerId);
            var modalManager = new ModalManager();

            // サーバー接続・アセットロード・シーンロードを並列実行し結果を受け取る
            // Run server connection, asset load, and scene load in parallel and collect results
            var serverInitializer = new ServerConnectionInitializer(_proprieties, loadingLog, loadingStopwatch, playerConnectionSetting);
            var modAssetLoader = new ModAssetLoader(serverDirectory, missingBlockIdObject, blockIconImagePhotographer, loadingLog, loadingStopwatch);
            var sceneLoader = new MainGameSceneLoader(loadingLog, loadingStopwatch);

            ServerConnectionResult serverResult;
            ModAssetLoadResult assetResult;
            AsyncOperation sceneLoadTask;
            try
            {
                (serverResult, assetResult, sceneLoadTask) = await UniTask.WhenAll(serverInitializer.RunAsync(), modAssetLoader.RunAsync(), sceneLoader.RunAsync());
            }
            catch (Exception e)
            {
                Debug.LogError($"初期化処理中にエラーが発生しました: {e.GetType()} {e.Message}\n{e.StackTrace}");
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                return;
            }

            // 取得結果から通信フォーマッタと静的コンテキストを初期化する
            // Initialize the message formatter and static context from the collected results
            MessagePackInitializer.Initialize();
            new ClientContext(assetResult.BlockGameObjectPrefabContainer, assetResult.ItemImageContainer, assetResult.BlockImageContainer, assetResult.TrainCarImageContainer, assetResult.FluidImageContainer, playerConnectionSetting, serverResult.VanillaApi, modalManager);

            SceneManager.sceneLoaded += MainGameSceneLoaded;
            sceneLoadTask.allowSceneActivation = true;

            #region Internal

            void MainGameSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= MainGameSceneLoaded;
                FinalizeInitializationAsync().Forget();
            }

            async UniTask FinalizeInitializationAsync()
            {
                var starter = FindObjectOfType<MainGameStarter>();
                var resolver = starter.StartGame(serverResult.HandshakeResponse);
                new ClientDIContext(new DIContainer(resolver));

                // Web UIをHubへバインド
                // Bind the Web UI to the hub
                WebUiHost.Game.WebUiGameBinder.Bind();

                // ダウンキャストして初期化専用メソッドを呼ぶ
                // Downcast and call the initialization-specific method
                (serverResult.VanillaApi.Event as VanillaApiEvent)?.InitializeDispatch();

                // ディスパッチ済み初期イベントの適用完了を全対象分待つ（順序契約により通常は即時完了する）
                // Wait until every target applies its dispatched initial events; normally instant per the ordering contract
                await WaitAllInitialEventApplyAsync(resolver);

                // ログイン状態復元→初期化完了通知
                // Restore login state, then announce initialization
                starter.RestoreLoginState(serverResult.HandshakeResponse);
                GameInitializedEvent.FireGameInitialized();
            }

            async UniTask WaitAllInitialEventApplyAsync(IObjectResolver resolver)
            {
                var targets = resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>();
                var warnAt = Time.realtimeSinceStartup + 5f;
                var warned = false;
                while (!targets.All(t => t.IsInitialEventApplied))
                {
                    // 長時間未完了なら詰まっている対象を顕在化させる（待機自体は継続）
                    // Surface stuck targets after a while; keep waiting regardless
                    if (!warned && Time.realtimeSinceStartup >= warnAt)
                    {
                        warned = true;
                        var pending = string.Join(", ", targets.Where(t => !t.IsInitialEventApplied).Select(t => t.GetType().Name));
                        Debug.LogWarning($"[InitializeScenePipeline] 初期イベント適用が未完了のまま待機中: {pending}");
                    }
                    await UniTask.Yield();
                }
            }

            #endregion
        }

#if UNITY_EDITOR
        // ツールバーの「セーブをロード・保存しない再生ボタン」用に起動引数を上書きする
        // Override launch args for the toolbar "play without loading/saving" button
        private static void ApplySkipSaveLoadModeIfNeeded(InitializeProprieties proprieties)
        {
            if (!UnityEditor.SessionState.GetBool(SkipSaveLoadSessionKey, false)) return;

            // 存在しないセーブファイルを指定してロードを回避し、オートセーブも無効化する
            // Point to a non-existent save file to skip loading, and disable auto-save
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.SaveFilePath = $"no_save_play_mode_{Guid.NewGuid()}.json";
            settings.AutoSave = false;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }
#endif
    }
}

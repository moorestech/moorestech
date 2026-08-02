using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Modal;
using Client.Localization;
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
            // WebUI起動失敗でもゲーム本体を止めないが、uGUI廃止Phase1ではUIは表示されない
            // Web UI startup failure does not block gameplay, but Phase 1 retirement leaves the UI unavailable
            try
            {
                // 起動成否をWeb UIホスト状態へ伝え、失敗時はWeb UIを利用不可にする
                // Propagate startup success to the Web UI host state and leave Web UI unavailable on failure
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
            Editor.SkipSaveLoadPlayModeSettings.ApplyIfNeeded(_proprieties);
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

            // サーバー接続とアセットロードを並列実行し結果を受け取る
            // Run server connection and asset load in parallel and collect results
            var serverInitializer = new ServerConnectionInitializer(_proprieties, loadingLog, loadingStopwatch, playerConnectionSetting);
            var modAssetLoader = new ModAssetLoader(serverDirectory, missingBlockIdObject, blockIconImagePhotographer, loadingLog, loadingStopwatch);

            ServerConnectionResult serverResult;
            ModAssetLoadResult assetResult;
            // mod CSV・サーバー通信・アセットロードという外部境界の失敗をまとめて隔離する
            // Isolate failures from the external boundaries: mod CSV, server communication, and asset loading
            try
            {
                // マスタロード後に同一mod順でゲーム辞書を合成する
                // Merge game dictionaries in the same mod order after master loading
                Localize.MergeGameDictionaries(ServerContext.GetService<global::Mod.Loader.ModsResource>());

                (serverResult, assetResult) = await UniTask.WhenAll(serverInitializer.RunAsync(), modAssetLoader.RunAsync());
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
            new ClientContext(assetResult.BlockGameObjectPrefabContainer, assetResult.ItemImageContainer, assetResult.BlockImageContainer, assetResult.TrainCarImageContainer, assetResult.ConnectToolImageContainer, assetResult.FluidImageContainer, playerConnectionSetting, serverResult.VanillaApi, modalManager);

            // シーンロードは全アセットロード完了後に直列実行する
            // Load the scene serially, after every asset load has finished
            // 0.9保持中は後続Addressablesロードが永久に待つため並列プリロード禁止
            // Never preload in parallel: holding at 0.9 stalls later Addressables loads forever
            SceneManager.sceneLoaded += MainGameSceneLoaded;
            SceneManager.LoadSceneAsync(SceneConstant.MainGameSceneName, LoadSceneMode.Single);

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

    }
}

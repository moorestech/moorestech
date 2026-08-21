using System;
using System.Diagnostics;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Modal;
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

            // 生成ワールド起動引数を上書き
            // Override launch args for the generated-world play button
            Editor.GeneratedWorldPlayModeSettings.ApplyIfNeeded(_proprieties);
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

            // Scene有効化待ちがAsyncOperationキューを止める前に、列車Prefabを読み切る
            // Finish train prefab loads before deferred scene activation stalls the AsyncOperation queue
            var trainCarIconTargets = await ModAssetLoader.PreloadTrainCarIconTargetsAsync();
            Debug.Log($"[InitializeScenePipeline] train car preload completed {loadingStopwatch.Elapsed}");

            var playerConnectionSetting = new PlayerConnectionSetting(_proprieties.PlayerId);
            var modalManager = new ModalManager();

            // サーバー接続とアセットロードを並列実行し結果を受け取る
            // Run server connection and asset load in parallel and collect results
            var serverInitializer = new ServerConnectionInitializer(_proprieties, loadingLog, loadingStopwatch, playerConnectionSetting);
            var modAssetLoader = new ModAssetLoader(serverDirectory, missingBlockIdObject, blockIconImagePhotographer, trainCarIconTargets, loadingLog, loadingStopwatch);

            ServerConnectionResult serverResult;
            ModAssetLoadResult assetResult;
            // 辞書・通信・読込の外部境界を隔離する
            // Isolate the external boundaries for mod dictionaries, communication, and asset loading
            try
            {
                GameDictionaryComposer.Run();
                (serverResult, assetResult) = await UniTask.WhenAll(ConnectServerThenFetchTerrainAsync(), modAssetLoader.RunAsync());
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

            // 地形取得はハンドシェイクのLayoutメタが要るため接続完了に継続させる。他2ユニットとは並列のまま
            // Terrain fetch needs the handshake's layout meta, so it continues from the connection; the other two units stay parallel
            async UniTask<ServerConnectionResult> ConnectServerThenFetchTerrainAsync()
            {
                var connectionResult = await serverInitializer.RunAsync();
                var fetchedChunkCount = await new TerrainDataFetcher(connectionResult.VanillaApi.Response).RunAsync(connectionResult.HandshakeResponse.MapLayout);
                loadingLog.text += $"\n地形データ準備完了({fetchedChunkCount}チャンク取得)  {loadingStopwatch.Elapsed}";
                return connectionResult;
            }

            void MainGameSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= MainGameSceneLoaded;

                // Forget境界の例外を専用callbackで観測し、DI未構築のMainGameへ取り残さない
                // Observe the forgotten boundary through its dedicated callback so MainGame is never stranded without DI
                new MainGameInitializationFinalizer(serverResult, serverDirectory).RunAsync().Forget(exception =>
                {
                    Debug.LogError($"初期化処理中にエラーが発生しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
                    SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                });
            }

            #endregion
        }

    }
}

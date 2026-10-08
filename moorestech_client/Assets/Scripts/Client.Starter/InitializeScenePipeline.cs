using System;
using System.Diagnostics;
using System.Threading;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Starter.Identity;
using Client.Starter.Initialization;
using Client.Starter.Initialization.Progress;
using Client.Starter.Initialization.Refusal;
using Client.Starter.Initialization.Context;
using Client.Starter.Initialization.Scene;
using Cysharp.Threading.Tasks;
using Game.Context;
using Mooresmaster.Localization.Generated;
using Server.Boot;
using Server.Boot.Args;
using Server.Util.MessagePack;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
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
        private InitializeProprieties _proprieties = InitializeProprieties.CreateLocalServer();

        public void SetProperty(InitializeProprieties proprieties)
        {
            _proprieties = proprieties;
        }

        private void Start()
        {
            Initialize().Forget();
        }

        private async UniTask Initialize()
        {
            // 全開始経路で識別を確定し、タイトルの確認も直接起動の印も無ければメニューへ戻す
            // Publish identity on every boot path, then return to the menu without title confirmations or a direct-boot mark
            Client.PlaytestReceiver.Launch.PlaytestLaunchProfile.EnsureIdentityPublished();
            if (Playtest.TitleGates.PlaytestTitleGates.EvaluateStart(nameof(InitializeScenePipeline), out _) != Playtest.TitleGates.PlaytestStartVerdict.Passed) { SceneManager.LoadScene(SceneConstant.MainMenuSceneName); return; }
            // 新しい起動シーケンスの開始。前回セッションの終了ガードをここで戻す
            // A new boot sequence begins; clear the previous session's shutdown guard here
            GameShutdownEvent.ResetForNewSession();
            GameShutdownEvent.InstallApplicationQuitDeferral();
            // 正規の終了口を通らない終了（エディタのPlay停止）でも、正常終了の印が書かれるようにする
            // Ensures the clean-exit mark is written even for exits that skip the canonical path (an Editor play-stop)
            GameShutdownEvent.InstallUnannouncedExitNotice();
            // 前回セッションの出所印より先に起動オプションを確定する
            // Resolve launch arguments before capturing the current session origin
            Client.RemoteExec.RemoteExecLaunchOption.ResolveFromCommandLine(Environment.GetCommandLineArgs());
            Playtest.PreviousSessionStartupTasks.BeginCurrentSessionMarks();
            // Play終了で各await継続を打ち切る。Task系境界の継続がEditModeで再開しシーンを汚すのを防ぐ
            // Play-mode exit cancels every await so Task-based continuations never resume in EditMode and dirty the scene
            var exitToken = Application.exitCancellationToken;
            var webUiStarted = await Initialization.Boot.WebUiStartup.StartAsync(exitToken);
            // Web UI の実ポートが確定してから遠隔実行を有効化する
            // Activate remote exec after the Web UI's actual port is known
            Client.RemoteExec.RemoteExecActivation.ActivateIfRequested(webUiStarted, Client.WebUiHost.Boot.WebUiHost.KestrelPort);
            // 正常終了で遠隔実行の入口を撤去する。終了イベントを知らないClient.RemoteExecへここから配線する
            // A clean exit withdraws the remote-exec entry; Client.RemoteExec does not know the shutdown event, so it is wired from here
            // 無効な起動は入口を持たないので購読もしない（同じ置き場を使う他プロセスの入口に触る理由が無い）
            // A disabled boot owns no entry and does not subscribe, having no reason to touch another process's entry in the shared location
            if (Client.RemoteExec.RemoteExecLaunchOption.IsEnabled)
                GameShutdownEvent.OnGameShutdown.Subscribe(_ => Client.RemoteExec.RemoteExecActivation.Deactivate());

#if UNITY_EDITOR
            Editor.PlayModeLaunchOverrides.ApplyIfNeeded(_proprieties);
#endif
            var args = CliConvert.Parse<StartServerSettings>(_proprieties.CreateLocalServerArgs);
            var serverDirectory = args.ServerDataDirectory;

            // 退避はタイトル（直接起動ならここ）、終了印の書き手は上の最初のawait前。記録を集めるかもここで1度だけ決める（ADR 0060 裁定5・ADR 0065）
            // Salvage happens at the title (here for a direct boot) and the exit-mark writer before the first await above; whether to collect is decided once here too (ADR 0060 adjudication 5, ADR 0065)
            var collectsPlaytestRecords = Playtest.PlaytestRecordCollection.Decide(_proprieties.IsRemoteConnection);
            Playtest.PreviousSessionStartupTasks.RunAtStartup(collectsPlaytestRecords);

            var loadingStopwatch = new Stopwatch();
            loadingStopwatch.Start();
            var loadingProgressLog = new LoadingProgressLog(loadingLog, loadingStopwatch);

            // 身元が決まらなければアセットも読まずに拒否を出す。身元解決の呼び出し口はここ1つ
            // Refuse before loading any asset when the identity cannot be resolved; this is the only call site that resolves it
            var identity = LocalPlayerIdentityResolver.ResolveForThisProcess();
            if (identity.Refusal.HasValue)
            {
                await InitializationFailurePresenter.ShowRefusalAsync(identity.Refusal.Value, loadingProgressLog, exitToken);
                return;
            }

            // Addressablesを初期化する
            // Initialize Addressables
            var initializeHandle = Addressables.InitializeAsync();
            await initializeHandle.ToUniTask(cancellationToken: exitToken);

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

            // サーバー接続とアセットロードを並列実行し結果を受け取る
            // Run server connection and asset load in parallel and collect results
            var serverInitializer = new ServerConnectionInitializer(_proprieties, loadingProgressLog, identity.Identity, exitToken);
            var modAssetLoader = new ModAssetLoader(serverDirectory, missingBlockIdObject, blockIconImagePhotographer, trainCarIconTargets, loadingProgressLog);

            ServerConnectionResult serverResult;
            ModAssetLoadResult assetResult;
            // 辞書・通信・読込の外部境界を隔離する
            // Isolate the external boundaries for mod dictionaries, communication, and asset loading
            try
            {
                GameDictionaryComposer.Run();
                (serverResult, assetResult) = await UniTask.WhenAll(ConnectServerThenFetchTerrainAsync(), modAssetLoader.RunAsync());
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // 失敗をログとUIへ出し、文言を読ませてからメインメニューへ戻す
                // Log the failure, surface it in the UI, and return to the main menu after the message is readable
                Debug.LogError($"初期化処理中にエラーが発生しました: {e.GetType()} {e.Message}\n{e.StackTrace}");
                await InitializationFailurePresenter.ShowInitializationFailedAsync(loadingProgressLog, exitToken);
                return;
            }

            // アセットロード完了後に通常の開始拒否を表示し、接続未成立の結果を後段へ渡さない
            // Surface an expected refusal after assets finish, before any context uses the absent connection
            if (serverResult.Refusal.HasValue)
            {
                await InitializationFailurePresenter.ShowRefusalAsync(serverResult.Refusal.Value, loadingProgressLog, exitToken);
                return;
            }

            // 取得結果から通信フォーマッタと静的コンテキストを初期化する
            // Initialize the message formatter and static context from the collected results
            MessagePackInitializer.Initialize();
            ClientContextComposer.Compose(assetResult, serverResult, blockIconImagePhotographer);

            // シーンロードは全アセットロード完了後に直列実行する
            // Load the scene serially, after every asset load has finished
            // 0.9保持中は後続Addressablesロードが永久に待つため並列プリロード禁止
            // Never preload in parallel: holding at 0.9 stalls later Addressables loads forever
            // Play終了後にここへ到達した継続はシーンロードで編集中シーンを壊すため確実に止める
            // A continuation reaching here after play-mode exit would clobber the edited scene, so stop it for certain
            exitToken.ThrowIfCancellationRequested();
            var sceneLoadedHandler = new MainGameSceneLoadedHandler(serverResult, serverDirectory, collectsPlaytestRecords, exitToken);
            SceneManager.sceneLoaded += sceneLoadedHandler.OnSceneLoaded;
            SceneManager.LoadSceneAsync(SceneConstant.MainGameSceneName, LoadSceneMode.Single);

            #region Internal

            // 地形取得はハンドシェイクのLayoutメタが要るため接続完了に継続させる。他2ユニットとは並列のまま
            // Terrain fetch needs the handshake's layout meta, so it continues from the connection; the other two units stay parallel
            async UniTask<ServerConnectionResult> ConnectServerThenFetchTerrainAsync()
            {
                var connectionResult = await serverInitializer.RunAsync();

                // 拒否は結果そのものが運ぶ。外側の変数へ写すと真実が2つになる
                // The refusal travels in the result itself; copying it into an outer variable would create a second truth
                if (connectionResult.Refusal.HasValue) return connectionResult;
                var fetchedChunkCount = await new TerrainDataFetcher(connectionResult.VanillaApi.Response, exitToken).RunAsync(connectionResult.HandshakeResponse.MapLayout);
                loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.TerrainReady, fetchedChunkCount.ToString());
                return connectionResult;
            }

            #endregion
        }
    }
}

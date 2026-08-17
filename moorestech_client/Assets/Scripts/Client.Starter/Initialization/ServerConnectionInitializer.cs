using System;
using System.Threading.Tasks;
using Client.Common;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Server.Boot;
using Server.Boot.Args;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// サーバーへ接続し VanillaApi を生成、初期ハンドシェイクまで行う
    /// Connects to the server, creates VanillaApi, and performs the initial handshake
    /// </summary>
    public class ServerConnectionInitializer
    {
        private readonly InitializeProprieties _proprieties;
        private readonly TMP_Text _loadingLog;
        private readonly System.Diagnostics.Stopwatch _loadingStopwatch;
        private readonly PlayerConnectionSetting _playerConnectionSetting;

        public ServerConnectionInitializer(InitializeProprieties proprieties, TMP_Text loadingLog, System.Diagnostics.Stopwatch loadingStopwatch, PlayerConnectionSetting playerConnectionSetting)
        {
            _proprieties = proprieties;
            _loadingLog = loadingLog;
            _loadingStopwatch = loadingStopwatch;
            _playerConnectionSetting = playerConnectionSetting;
        }

        public async UniTask<ServerConnectionResult> RunAsync()
        {
            //サーバーとの接続を確立
            var serverCommunicator = await ConnectionToServer();

            _loadingLog.text += $"\nサーバーとの接続完了  {_loadingStopwatch.Elapsed}";

            //データの受付開始
            var packetSender = new PacketSender(serverCommunicator);
            var exchangeManager = new PacketExchangeManager(packetSender);
            Task.Run(() => serverCommunicator.StartCommunicat(exchangeManager));

            //Vanilla APIの作成
            var vanillaApi = new VanillaApi(exchangeManager, packetSender, serverCommunicator, _playerConnectionSetting);

            //最初に必要なデータを取得
            // Fetch the initial data bundle
            var handshakeResponse = await vanillaApi.Response.InitialHandShake(_playerConnectionSetting.PlayerId, default);

            _loadingLog.text += $"\n初期データ取得完了  {_loadingStopwatch.Elapsed}";

            return new ServerConnectionResult { VanillaApi = vanillaApi, HandshakeResponse = handshakeResponse };

            #region Internal

            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                var timeOut = TimeSpan.FromSeconds(3);

                // リモートは明示指定の宛先のみ。失敗しても内蔵サーバーへフォールバックしない（ADR 0013）
                // Remote uses only the explicit destination and never falls back to the embedded server (ADR 0013)
                if (_proprieties.IsRemoteConnection)
                {
                    // サーバー接続はネットワーク境界のため失敗を隔離しメニューへ復帰する
                    // Server connect is a network boundary; isolate failures and return to the menu
                    try
                    {
                        var serverProperties = new ConnectionServerProperties(_proprieties.ServerIp, _proprieties.ServerPort);
                        return await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                    }
                    catch (Exception e)
                    {
                        await HandleConnectionFailure(e);
                        throw;
                    }
                }

                // ローカルは接続試行なしで必ず内蔵サーバーを起動する
                // Local play always boots the embedded server without probing
                // 内蔵サーバー起動と接続はプロセス/ネットワーク境界のため失敗を隔離しメニューへ復帰する
                // Embedded server launch and connect are process/network boundaries; isolate failures and return to the menu
                try
                {
                    var serverInstanceGameObject = new GameObject("ServerInstance");
                    var serverStarter = serverInstanceGameObject.AddComponent<ServerStarter>();

                    // ポート未指定なら0(OS自動採番)を渡し、実行時に空きポートへバインドさせる
                    // Pass 0 (OS auto-assign) when no port is specified, so the server binds a free port at runtime
                    var localServerSettings = CliConvert.Parse<StartServerSettings>(_proprieties.CreateLocalServerArgs ?? Array.Empty<string>());
                    localServerSettings.Port ??= 0;
                    serverStarter.SetArgs(CliConvert.Serialize(localServerSettings));
                    UnityEngine.Object.DontDestroyOnLoad(serverInstanceGameObject);

                    // バインド完了を待ち、実際に割り当てられたポートへ接続する
                    // Wait for binding to complete, then connect to the actually assigned port
                    await UniTask.WaitUntil(() => serverStarter.BoundPort != 0).Timeout(TimeSpan.FromSeconds(60));
                    var localServerProperties = new ConnectionServerProperties(_proprieties.ServerIp, serverStarter.BoundPort);

                    return await ServerCommunicator.CreateConnectedInstance(localServerProperties).Timeout(timeOut);
                }
                catch (Exception e)
                {
                    await HandleConnectionFailure(e);
                    throw;
                }
            }

            // 失敗をログとUIへ出しメインメニューへ戻す共通復帰処理
            // Shared recovery path: log the failure, show it in the UI, and return to the main menu
            async UniTask HandleConnectionFailure(Exception e)
            {
                Debug.LogError($"サーバーへの接続に失敗しました: {e.Message}");
                _loadingLog.text += "\nサーバーへの接続に失敗しました。メインメニューに戻ります。";
                await UniTask.Delay(2000);
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
            }

            #endregion
        }
    }

    /// <summary>
    /// サーバー接続初期化の結果
    /// Result of the server connection initialization
    /// </summary>
    public class ServerConnectionResult
    {
        public VanillaApi VanillaApi;
        public InitialHandshakeResponse HandshakeResponse;
    }
}

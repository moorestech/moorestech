using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Client.Common;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Server.Boot;
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
            var vanillaApi = new VanillaApi(exchangeManager, packetSender, serverCommunicator, _playerConnectionSetting, _proprieties.LocalServerProcess);

            //最初に必要なデータを取得
            // Fetch the initial data bundle
            var handshakeResponse = await vanillaApi.Response.InitialHandShake(_playerConnectionSetting.PlayerId, default);

            _loadingLog.text += $"\n初期データ取得完了  {_loadingStopwatch.Elapsed}";

            return new ServerConnectionResult { VanillaApi = vanillaApi, HandshakeResponse = handshakeResponse };

            #region Internal

            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                var serverProperties = new ConnectionServerProperties(_proprieties.ServerIp, _proprieties.ServerPort);
                var timeOut = TimeSpan.FromSeconds(3);
                // サーバー接続はネットワーク境界。失敗を捕捉してローカルサーバー起動へフォールバックする
                // Server connect is a network boundary; catch failures to fall back to launching a local server
                try
                {
                    // 10秒以内にサーバー接続できなければタイムアウト
                    var serverCommunicator = await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                    return serverCommunicator;
                }
                catch (SocketException)
                {
                    _loadingLog.text += "\nサーバーの接続が失敗しました。サーバーを起動します。";
                    // ローカルサーバー起動と再接続もネットワーク/プロセス境界のため隔離する
                    // Local server launch and reconnect are also network/process boundaries, so isolate them
                    try
                    {
                        var serverInstanceGameObject = new GameObject("ServerInstance");
                        var serverStarter = serverInstanceGameObject.AddComponent<ServerStarter>();
                        if (_proprieties.CreateLocalServerArgs != null)
                        {
                            serverStarter.SetArgs(_proprieties.CreateLocalServerArgs);
                        }
                        UnityEngine.Object.DontDestroyOnLoad(serverInstanceGameObject);

                        await UniTask.Delay(1000);

                        var serverCommunicator = await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                        return serverCommunicator;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"サーバーへの接続に失敗しました: {e.Message}");
                        _loadingLog.text += "\nサーバーへの接続に失敗しました。メインメニューに戻ります。";
                        await UniTask.Delay(2000);
                        SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                        throw;
                    }
                }
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

using System;
using System.Threading;
using System.Threading.Tasks;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Server.Boot;
using Server.Boot.Args;
using TMPro;
using UnityEngine;

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

        // 起動した内蔵サーバー。リモート接続では起動しないためnullのまま
        // The embedded server that was started; stays null for remote connections
        public ServerStarter EmbeddedServer { get; private set; }

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

            return new ServerConnectionResult { VanillaApi = vanillaApi, HandshakeResponse = handshakeResponse, EmbeddedServer = EmbeddedServer };

            #region Internal

            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                var timeOut = TimeSpan.FromSeconds(3);

                // リモートは明示指定の宛先のみ。失敗しても内蔵サーバーへフォールバックしない（ADR 0013）
                // Remote uses only the explicit destination and never falls back to the embedded server (ADR 0013)
                if (_proprieties.IsRemoteConnection)
                {
                    var serverProperties = new ConnectionServerProperties(_proprieties.ServerIp, _proprieties.RemoteServerPort);
                    return await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                }

                // ローカルは試行せず内蔵サーバー起動
                // Local boots the embedded server without probing
                var serverInstanceGameObject = new GameObject("ServerInstance");
                var serverStarter = serverInstanceGameObject.AddComponent<ServerStarter>();
                EmbeddedServer = serverStarter;

                // 0でOS自動割り当てさせる
                // 0 means OS auto-assigns the port
                var localServerSettings = CliConvert.Parse<StartServerSettings>(_proprieties.CreateLocalServerArgs);
                localServerSettings.Port ??= 0;
                serverStarter.SetArgs(CliConvert.Serialize(localServerSettings));
                UnityEngine.Object.DontDestroyOnLoad(serverInstanceGameObject);

                // バインド後に実ポートへ接続。タイムアウト時も述語をPlayerLoopに残さない
                // Connect to the assigned port after binding, leaving no predicate in the PlayerLoop on timeout
                using var boundPortWait = new CancellationTokenSource();
                await UniTask.WaitUntil(() => serverStarter.BoundPort != 0, PlayerLoopTiming.Update, boundPortWait.Token)
                    .Timeout(TimeSpan.FromSeconds(60), taskCancellationTokenSource: boundPortWait);
                var localServerProperties = new ConnectionServerProperties(_proprieties.ServerIp, serverStarter.BoundPort);

                return await ServerCommunicator.CreateConnectedInstance(localServerProperties).Timeout(timeOut);
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

        // 内蔵サーバー。リモート接続時はnullで、破棄責務は受け取った上位が持つ
        // The embedded server; null for remote connections, and the receiving upper layer owns its destruction
        public ServerStarter EmbeddedServer;
    }
}

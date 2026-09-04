using System;
using System.Threading;
using System.Threading.Tasks;
using Client.Game.Common;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Server.Boot;
using Server.Boot.Args;
using Client.Starter.Initialization.Progress;
using UnityEngine;
using Mooresmaster.Localization.Generated;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// サーバーへ接続し VanillaApi を生成、初期ハンドシェイクまで行う
    /// Connects to the server, creates VanillaApi, and performs the initial handshake
    /// </summary>
    public class ServerConnectionInitializer
    {
        private readonly InitializeProprieties _proprieties;
        private readonly LoadingProgressLog _loadingProgressLog;
        private readonly PlayerConnectionSetting _playerConnectionSetting;

        public ServerConnectionInitializer(InitializeProprieties proprieties, LoadingProgressLog loadingProgressLog, PlayerConnectionSetting playerConnectionSetting)
        {
            _proprieties = proprieties;
            _loadingProgressLog = loadingProgressLog;
            _playerConnectionSetting = playerConnectionSetting;
        }

        public async UniTask<ServerConnectionResult> RunAsync()
        {
            //サーバーとの接続を確立
            var serverCommunicator = await ConnectionToServer();

            _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.ServerConnected);

            //データの受付開始
            var packetSender = new PacketSender(serverCommunicator);
            var exchangeManager = new PacketExchangeManager(packetSender);
            Task.Run(() => serverCommunicator.StartCommunicat(exchangeManager));

            //Vanilla APIの作成
            var vanillaApi = new VanillaApi(exchangeManager, packetSender, serverCommunicator, _playerConnectionSetting);

            // リモートは内蔵サーバーを持たないため、通信越しに書き出し完了を待つ参加者を立てる
            // A remote connection owns no embedded server, so register a participant that awaits the flush over the wire
            if (_proprieties.IsRemoteConnection) GameShutdownEvent.RegisterParticipant(new RemoteServerSaveFlushParticipant(vanillaApi));

            //最初に必要なデータを取得
            // Fetch the initial data bundle
            var handshakeResponse = await vanillaApi.Response.InitialHandShake(_playerConnectionSetting.PlayerId, default);

            _loadingProgressLog.AppendElapsed(LocalizationKeys.Ui.Loading.InitialDataFetched);

            return new ServerConnectionResult { VanillaApi = vanillaApi, HandshakeResponse = handshakeResponse };

            #region Internal

            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                var timeOut = TimeSpan.FromSeconds(3);

                // リモートは明示指定の宛先のみ。失敗しても内蔵サーバーへフォールバックしない（ADR 0013）
                // Remote uses only the explicit destination and never falls back to the embedded server (ADR 0013)
                if (_proprieties.IsRemoteConnection)
                {
                    var serverProperties = new ConnectionServerProperties(_proprieties.ServerIp, _proprieties.RemoteServerPort.Value);

                    // タイムアウト時に接続タスクとソケットを道連れに畳む
                    // Fold the connection task and its socket together on timeout
                    using var remoteConnectWait = new CancellationTokenSource();
                    return await ServerCommunicator.CreateConnectedInstance(serverProperties, remoteConnectWait.Token)
                        .Timeout(timeOut, taskCancellationTokenSource: remoteConnectWait);
                }

                // ローカルは試行せず内蔵サーバー起動
                // Local boots the embedded server without probing
                var serverInstanceGameObject = new GameObject("ServerInstance");
                var serverStarter = serverInstanceGameObject.AddComponent<ServerStarter>();

                // 生成した内蔵サーバーは終了パイプラインの参加者として自壊する。所有ハンドルを外へ配らない
                // The embedded server folds itself as a shutdown participant, so no ownership handle leaves this scope
                GameShutdownEvent.RegisterParticipant(new EmbeddedServerShutdownParticipant(serverStarter));

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

                // ローカル接続も同じくタイムアウトでタスクとソケットを残さない
                // The local connection likewise leaves no task or socket behind on timeout
                using var localConnectWait = new CancellationTokenSource();
                return await ServerCommunicator.CreateConnectedInstance(localServerProperties, localConnectWait.Token)
                    .Timeout(timeOut, taskCancellationTokenSource: localConnectWait);
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

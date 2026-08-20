using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Core.Master;
using Game.PlayerConnection;
using Core.Update;
using Game.Context;
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Game.SaveLoad.Interface;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Config;
using Mod.Loader;
using Server.Boot.Args;
using Server.Boot.Loop;
using Server.Boot.Loop.PacketProcessing;
using Server.Event;
using UnityEngine;

namespace Server.Boot
{
    public class ServerInstanceManager : IDisposable
    {
        private const int DefaultGeneratedSeed = 196;

        private Thread _connectionUpdateThread;
        private Thread _gameUpdateThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Socket _listener;

        private readonly string[] _args;

        // 実際にバインドされた待ち受けポート。バインド前は0
        // The actually bound listen port; 0 before binding
        public int BoundPort => _listener == null ? 0 : ((IPEndPoint)_listener.LocalEndPoint).Port;

        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }

        public void Start()
        {
            (_connectionUpdateThread, _gameUpdateThread, _cancellationTokenSource, _listener) = Start(_args);
        }

        private static (Thread connectionUpdateThread, Thread gameUpdateThread, CancellationTokenSource cancellationTokenSource, Socket listener) Start(string[] args)
        {
            // 起動引数からワールドディレクトリのルートを解決する
            // Resolve the world directory root from launch arguments
            var settings = CliConvert.Parse<StartServerSettings>(args);
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(settings.WorldDirectory);

            // 生成設定はマスタなのでプロビジョニング前にマスタをロードする（Create()内の再ロードは冪等）
            // Generation config lives in master data, so load masters before provisioning (reload in Create() is idempotent)
            var modResource = new ModsResource(Path.Combine(settings.ServerDataDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

            // generatedモードの未指定シードを固定し、同じマスタから常に同じワールドを生成する
            // Fix the unspecified generated-mode seed so the same master always produces the same world
            // 明示指定なら0も含めそのまま使い、templateモードの従来値0も維持する
            // Preserve every explicit value including zero, as well as template mode's existing zero
            var seed = settings.Seed ?? (settings.MapMode == WorldProvisioner.GeneratedMapMode ? DefaultGeneratedSeed : 0);

            // ワールドディレクトリをDI構築前に整備する（無ければ生成/テンプレートコピー）
            // Provision the world directory before DI container construction
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, settings.ServerDataDirectory, settings.MapMode, seed));

            var serverDirectory = settings.ServerDataDirectory;
            var options = new MoorestechServerDIContainerOptions(serverDirectory)
                {
                    worldDataDirectory = worldDataDirectory,
                };

            Debug.Log("データをロードします　パス:" + serverDirectory);
            
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            
            //マップをロードする
            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

            //初期ロード完了後にIPostLoadInitializableのLoadを一括で呼ぶ。ロード中の設置等はクライアントへ配信しない
            //Invoke Load on all IPostLoadInitializable implementations after initial load, so load-time placements etc. are not sent to clients
            foreach (var postLoadInitializable in serviceProvider.GetServices<IPostLoadInitializable>()) postLoadInitializable.Load();

            //modのOnLoadコードを実行する
            var modsResource = serviceProvider.GetService<ModsResource>();
            modsResource.Mods.ToList().ForEach(
                m => m.Value.ModEntryPoints.ForEach(
                    e =>
                    {
                        Debug.Log("Modをロードしました modId:" + m.Value + " className:" + e.GetType().Name);
                        e.OnLoad(new ServerModEntryInterface(serviceProvider, packet));
                    }));
            
            
            //サーバーの起動とゲームアップデートの開始
            var cancellationToken = new CancellationTokenSource();
            var token = cancellationToken.Token;
            var connectionRegistry = (PlayerConnectionRegistry)serviceProvider.GetService<IPlayerConnectionChecker>();
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var tickEndPacketQueue = serviceProvider.GetRequiredService<TickEndPacketQueue>();

            // 起動設定のポートで待ち受けソケットをバインドする
            // Bind the listen socket with the configured port
            var listener = ServerListenAcceptor.CreateBoundListener(settings.Port);

            // パケットキュープロセッサを作成してメインスレッドで処理を開始
            var connectionUpdateThread = new Thread(() =>
                ServerListenAcceptor.StartServer(listener, packet, connectionRegistry, eventProtocolProvider, tickEndPacketQueue, token));
            connectionUpdateThread.Name = "[moorestech]通信受け入れスレッド";
            connectionUpdateThread.Start();
            
            if (settings.AutoSave)
            {
                Task.Run(() => AutoSaveSystem.AutoSave(serviceProvider.GetRequiredService<IWorldSaveRequest>(), token), cancellationToken.Token);
            }
            // アップデートのタスク名を設定
            var gameUpdateThread = new Thread(() => ServerGameUpdater.StartUpdate(token));
            gameUpdateThread.Name = "[moorestech]ゲームアップデートスレッド";
            gameUpdateThread.Start();

            return (connectionUpdateThread, gameUpdateThread, cancellationToken, listener);
        }
        
        
        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                _connectionUpdateThread?.Abort();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            // ネットワーク境界のソケット破棄。閉じ損ねるとポートが解放されないため隔離して確実に閉じる
            // Socket teardown at the network boundary; isolate so the port is always released
            try
            {
                _listener?.Close();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                _gameUpdateThread?.Abort();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                GameUpdater.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

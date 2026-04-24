using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Loader;
using Server.Boot.Args;
using Server.Boot.Loop;
using Server.Boot.Shutdown;
using UnityEngine;

namespace Server.Boot
{
    public class ServerInstanceManager
    {
        private static readonly TimeSpan ThreadJoinTimeout = TimeSpan.FromSeconds(3);

        private Thread _connectionUpdateThread;
        private Thread _gameUpdateThread;
        private CancellationTokenSource _cancellationTokenSource;

        private readonly string[] _args;

        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }

        public void Start()
        {
            (_connectionUpdateThread, _gameUpdateThread, _cancellationTokenSource) = StartInternal(_args);

            // 終了パイプラインに接続停止・アップデート停止・サブシステム破棄を登録
            // Register stop-accepting, stop-update, and subsystem dispose into the shutdown pipeline
            ShutdownCoordinator.Register(ShutdownPhase.StopAcceptingConnections, "Server.CancelTokens",
                () => { _cancellationTokenSource?.Cancel(); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "Server.JoinThreads", JoinThreadsAsync);
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "Server.GameUpdater.Dispose",
                () => { GameUpdater.Dispose(); return UniTask.CompletedTask; });
        }

        // 両スレッドは CancellationToken を監視しているので Cancel 後の自然終了を並列に待つ
        // Both threads observe CancellationToken; wait for natural exit in parallel after Cancel
        private async UniTask JoinThreadsAsync()
        {
            await UniTask.WhenAll(
                UniTask.RunOnThreadPool(() => JoinOne(_connectionUpdateThread, "connection update thread")),
                UniTask.RunOnThreadPool(() => JoinOne(_gameUpdateThread, "game update thread")));
        }

        private static void JoinOne(Thread thread, string label)
        {
            if (thread != null && !thread.Join(ThreadJoinTimeout))
                Debug.LogWarning($"[ServerInstanceManager] {label} did not exit within timeout");
        }

        private static (Thread connectionUpdateThread, Thread gameUpdateThread, CancellationTokenSource cancellationTokenSource) StartInternal(string[] args)
        {
            var settings = CliConvert.Parse<StartServerSettings>(args);
            var serverDirectory = settings.ServerDataDirectory;
            var options = new MoorestechServerDIContainerOptions(serverDirectory)
                {
                    saveJsonFilePath = new SaveJsonFilePath(settings.SaveFilePath),
                };

            Debug.Log("データをロードします　パス:" + serverDirectory);

            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);

            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

            var modsResource = serviceProvider.GetService<ModsResource>();
            modsResource.Mods.ToList().ForEach(
                m => m.Value.ModEntryPoints.ForEach(
                    e =>
                    {
                        Debug.Log("Modをロードしました modId:" + m.Value + " className:" + e.GetType().Name);
                        e.OnLoad(new ServerModEntryInterface(serviceProvider, packet));
                    }));

            var cancellationToken = new CancellationTokenSource();
            var token = cancellationToken.Token;

            var connectionUpdateThread = new Thread(() => new ServerListenAcceptor().StartServer(packet, token));
            connectionUpdateThread.Name = "[moorestech]通信受け入れスレッド";
            connectionUpdateThread.Start();

            if (settings.AutoSave)
            {
                Task.Run(() => AutoSaveSystem.AutoSave(serviceProvider.GetService<IWorldSaveDataSaver>(), token), cancellationToken.Token);
            }

            var gameUpdateThread = new Thread(() => ServerGameUpdater.StartUpdate(token));
            gameUpdateThread.Name = "[moorestech]ゲームアップデートスレッド";
            gameUpdateThread.Start();

            return (connectionUpdateThread, gameUpdateThread, cancellationToken);
        }
    }
}

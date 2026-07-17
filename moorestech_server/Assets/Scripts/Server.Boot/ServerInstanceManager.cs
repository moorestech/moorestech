using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Game.PlayerConnection;
using Core.Update;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Loader;
using Server.Boot.Args;
using Server.Boot.Loop;
using UnityEngine;

namespace Server.Boot
{
    public class ServerInstanceManager : IDisposable
    {
        private Thread _connectionUpdateThread;
        private Thread _gameUpdateThread;
        private CancellationTokenSource _cancellationTokenSource;
        private WorldSaveCoordinator _worldSaveCoordinator;

        private readonly string[] _args;

        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }

        public void Start()
        {
            (_connectionUpdateThread, _gameUpdateThread, _cancellationTokenSource, _worldSaveCoordinator) = Start(_args);
        }

        private static (Thread connectionUpdateThread, Thread gameUpdateThread, CancellationTokenSource cancellationTokenSource, WorldSaveCoordinator worldSaveCoordinator) Start(string[] args)
        {
            // これはコンパイルエラーを避ける仮対応
            var settings = CliConvert.Parse<StartServerSettings>(args);
            
            //カレントディレクトリを表示
            var serverDirectory = settings.ServerDataDirectory;
            var options = new MoorestechServerDIContainerOptions(serverDirectory)
                {
                    saveJsonFilePath = new SaveJsonFilePath(settings.SaveFilePath),
                };
            
            Debug.Log("データをロードします　パス:" + serverDirectory);
            
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            
            //マップをロードする
            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
            
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
            
            // パケットキュープロセッサを作成してメインスレッドで処理を開始
            var connectionUpdateThread = new Thread(() => new ServerListenAcceptor().StartServer(packet, connectionRegistry, token));
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

            return (connectionUpdateThread, gameUpdateThread, cancellationToken, serviceProvider.GetRequiredService<WorldSaveCoordinator>());
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
                // tickスレッドはキャンセルを受けてtick境界で自然停止する。Abortはタイムアウト時のfallback
                // The tick thread stops itself at a tick boundary after cancellation; Abort is only a timeout fallback
                if (_gameUpdateThread != null && !_gameUpdateThread.Join(TimeSpan.FromSeconds(1))) _gameUpdateThread.Abort();
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
            try
            {
                // tickスレッド停止後は安定点なので、未処理のセーブ要求を同期フラッシュして終了時の消失を防ぐ
                // After the tick thread stops this is a stable point; flush any pending save request so shutdown never drops it
                _worldSaveCoordinator?.SaveIfRequested();
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

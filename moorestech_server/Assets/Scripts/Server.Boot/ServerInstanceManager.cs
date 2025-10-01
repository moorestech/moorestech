using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
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
        private CancellationTokenSource _cancellationTokenSource;
        private PacketHandler _packetHandler;

        private readonly string[] _args;
        
        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }
        
        public void Start()
        {
            (_connectionUpdateThread, _cancellationTokenSource, _packetHandler) = Start(_args);
        }

        private static (Thread connectionUpdateThread, CancellationTokenSource cancellationTokenSource, PacketHandler packetHandler) Start(string[] args)
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

            var packetHandler = new PacketHandler();
            var connectionUpdateThread = new Thread(() => packetHandler.StartServer(packet, token));
            connectionUpdateThread.Name = "[moorestech]通信受け入れスレッド";
            connectionUpdateThread.Start();
            
            if (settings.AutoSave)
            {
                Task.Run(() => AutoSaveSystem.AutoSave(serviceProvider.GetService<IWorldSaveDataSaver>(), token), cancellationToken.Token);
            }
            Task.Run(() => ServerGameUpdater.StartUpdate(token), cancellationToken.Token);

            return (connectionUpdateThread, cancellationToken, packetHandler);
        }
        
        
        public void Dispose()
        {
            try
            {
                // CancellationTokenをキャンセルして、各種タスクに終了を通知
                _cancellationTokenSource?.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                // PacketHandlerのlistenerソケットをクローズ
                // これによりAccept()がSocketExceptionを投げて通信スレッドが終了する
                _packetHandler?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                // 通信スレッドが終了するまで最大5秒間待機
                // Join()は指定したスレッドの終了を待つメソッド
                // タイムアウトを指定することで、スレッドが終了しない場合でも処理を続行できる
                _connectionUpdateThread?.Join(TimeSpan.FromSeconds(5));
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
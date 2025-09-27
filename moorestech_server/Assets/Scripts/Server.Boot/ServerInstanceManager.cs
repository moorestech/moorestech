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
        
        private readonly string[] _args;
        
        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }
        
        public void Start()
        {
            (_connectionUpdateThread, _cancellationTokenSource) = Start(_args);
        }
        
        private static (Thread connectionUpdateThread, CancellationTokenSource cancellationTokenSource) Start(string[] args)
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
            
            var connectionUpdateThread = new Thread(() => new PacketHandler().StartServer(packet, token));
            connectionUpdateThread.Name = "[moorestech]通信受け入れスレッド";
            connectionUpdateThread.Start();
            
            if (settings.AutoSave)
            {
                Task.Run(() => AutoSaveSystem.AutoSave(serviceProvider.GetService<IWorldSaveDataSaver>(), token), cancellationToken.Token);
            }
            Task.Run(() => ServerGameUpdater.StartUpdate(token), cancellationToken.Token);
            
            return (connectionUpdateThread, cancellationToken);
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
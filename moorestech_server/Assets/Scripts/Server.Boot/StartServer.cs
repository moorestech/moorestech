using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
using Game.Save.Interface;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Loader;
using Server.Boot.PacketHandle;
using UnityEngine;

namespace Server.Boot
{
    public static class StartServer
    {
        private const int argsCount = 1;


        private static string DebugServerDirectory => Path.GetFullPath(Path.Combine(Environment.CurrentDirectory ,"../moorestech_client/Server"));

        private static string StartupFromClientFolderPath
        {
            get
            {
                var di = new DirectoryInfo(Environment.CurrentDirectory);
                return Path.Combine(di.FullName, "server", "mods");
            }
        }

        public static (Thread serverUpdateThread, Thread gameUpdateThread, CancellationTokenSource autoSaveTokenSource) Start(string[] args)
        {
            //カレントディレクトリを表示
#if DEBUG
            var serverDirectory = DebugServerDirectory;
#else
            var serverDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#endif

            Debug.Log("データをロードします　パス:" + serverDirectory);

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(serverDirectory);

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
            var serverUpdateThread = new Thread(() => new PacketHandler().StartServer(packet));
            var gameUpdateThread = new Thread(() =>
            {
                while (true) GameUpdater.Update();
            });

            var autoSaveTaskTokenSource = new CancellationTokenSource();
            Task.Run(() => new AutoSaveSystem(serviceProvider.GetService<IWorldSaveDataSaver>()).AutoSave(autoSaveTaskTokenSource), autoSaveTaskTokenSource.Token);

            return (serverUpdateThread, gameUpdateThread, autoSaveTaskTokenSource);
        }
    }
}
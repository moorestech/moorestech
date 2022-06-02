using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
using Game.Save.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.StartServerSystem.PacketHandle;

namespace Server.StartServerSystem
{
    public static class StartServer
    {
        private const int argsCount = 1;
        
        public static async Task Start(string[] args)
        {
            try
            {
#if DEBUG
                var configPath = DebugModsDirectory;
#else
                var configPath = ReleasesModsDirectory;
                if (args.Length == 0)
                {
                    Console.WriteLine("コマンドライン引数にコンフィグのパスが指定されていませんでした。デフォルトコンフィグパスを使用します。");
                } 
                else if(args[0] == "startupFromClient")
                {
                    configPath = StartupFromClientFolderPath;
                }
                else
                {
                    configPath = args[0];
                }
#endif
                
                var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(configPath);
                
                //マップをロードする
                serviceProvider.GetService<ILoadRepository>().Load();
                Console.WriteLine("マップをロード又は新規作成します。");
                
                //サーバーの起動とゲームアップデートの開始
                new Thread(() => new PacketHandler().StartServer(packet)).Start();
                new Thread(() => { while (true) { GameUpdate.Update(); } }).Start();

                await new AutoSaveSystem(serviceProvider.GetService<ISaveRepository>()).AutoSave();

                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine("StackTrace");
                Console.WriteLine(e.StackTrace);
                
                Console.WriteLine();
                Console.WriteLine("Message");
                
                Console.WriteLine(e.Message);
                Console.ReadKey();
            }
        }

        
        private static string DebugModsDirectory
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Server.Starter", "mods");
            }
        }
        private static string ReleasesModsDirectory
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                return Path.Combine(di.FullName, "mods");
            }
        }
        
        private static string StartupFromClientFolderPath
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                return Path.Combine(di.FullName,"server", "Config");
            }
        }
    }
}
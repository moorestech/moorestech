using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Core.Update;
using Game.Save.Interface;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Loader;
using Server.Boot.PacketHandle;

namespace Server.Boot
{
    public static class StartServer
    {
        private const int argsCount = 1;
        
        public static async Task Start(string[] args)
        {
            try
            {
#if DEBUG
                var modsDirectory = DebugModsDirectory;
#else
                var modsDirectory = ReleasesModsDirectory;
                if (args.Length == 0)
                {
                    Console.WriteLine("コマンドライン引数にコンフィグのパスが指定されていませんでした。デフォルトコンフィグパスを使用します。");
                } 
                else if(args[0] == "startupFromClient")
                {
                    modsDirectory = StartupFromClientFolderPath;
                }
                else
                {
                    modsDirectory = args[0];
                }
#endif
                
                var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(modsDirectory);
                
                //マップをロードする
                serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
                
                //modのOnLoadコードを実行する
                var modsResource = serviceProvider.GetService<ModsResource>();
                modsResource.Mods.ToList().ForEach(
                    m => m.Value.ModEntryPoints.ForEach(
                        e => e.OnLoad(new ServerModEntryInterface(serviceProvider,packet))));
                
                
                //サーバーの起動とゲームアップデートの開始
                new Thread(() => new PacketHandler().StartServer(packet)).Start();
                new Thread(() => { while (true) { GameUpdate.Update(); } }).Start();

                await new AutoSaveSystem(serviceProvider.GetService<IWorldSaveDataSaver>()).AutoSave();

                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("StackTrace");
                Console.WriteLine(e.StackTrace);
                
                Console.WriteLine();
                Console.WriteLine("Message");
                
                Console.WriteLine(e.Message);
                Console.ReadKey();
            }
        }

        
        //環境変数を取得する
        private static string DebugModsDirectory
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("MOORES_MODS_DIRECTORY");
                if (path != null)
                {
                    return path;
                }
                
                Console.WriteLine("環境変数にコンフィグのパスが指定されていませんでした。MOORES_MODS_DIRECTORYを設定してください。");
                Console.WriteLine("Windowsの場合の設定コマンド > setx /M MOORES_MODS_DIRECTORY \"C:～ \"");
                Console.WriteLine("Macの場合の設定コマンド > export MOORES_MODS_DIRECTORY=\"～\"");
                return ReleasesModsDirectory;
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
                return Path.Combine(di.FullName,"server", "mods");
            }
        }
    }
}
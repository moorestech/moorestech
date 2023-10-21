using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
using Game.Save.Interface;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Loader;
using Server.Boot.PacketHandle;

namespace Server.Boot
{
    public static class StartServer
    {
        private const int argsCount = 1;


        private static string DebugServerDirectory
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("MOORES_SERVER_DIRECTORY");
                if (path != null) return path;

                
                Console.WriteLine("。MOORES_SERVER_DIRECTORY。");
                Console.WriteLine("Windows > setx /M MOORES_SERVER_DIRECTORY \"C:～ \"");
                Console.WriteLine("Mac > export MOORES_SERVER_DIRECTORY=\"～\"");
                return Environment.CurrentDirectory;
            }
        }

        private static string StartupFromClientFolderPath
        {
            get
            {
                var di = new DirectoryInfo(Environment.CurrentDirectory);
                return Path.Combine(di.FullName, "server", "mods");
            }
        }

        public static async Task Start(string[] args)
        {
            try
            {
#if DEBUG
                var serverDirectory = DebugServerDirectory;
#else
                var serverDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#endif

                Console.WriteLine("　:" + serverDirectory);

                var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(serverDirectory);

                
                serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

                //modOnLoad
                var modsResource = serviceProvider.GetService<ModsResource>();
                modsResource.Mods.ToList().ForEach(
                    m => m.Value.ModEntryPoints.ForEach(
                        e =>
                        {
                            Console.WriteLine("Mod modId:" + m.Value + " className:" + e.GetType().Name);
                            e.OnLoad(new ServerModEntryInterface(serviceProvider, packet));
                        }));


                
                new Thread(() => new PacketHandler().StartServer(packet)).Start();
                new Thread(() =>
                {
                    while (true) GameUpdater.Update();
                }).Start();

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
    }
}
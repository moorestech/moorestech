using System;
using System.IO;
using System.Threading;
using Core.Item;
using Core.Update;
using Microsoft.Extensions.DependencyInjection;
using PlayerInventory;
using Server.Event;
using Server.PacketHandle;
using World;
using World.Event;

namespace Server
{
    public static class StartServer
    {
        private const int argsCount = 1;
        
        public static void Main(string[] args)
        {
#if DEBUG
            args = new string[1];
            args[0] = DebugConfigPath.FolderPath;
#else
            var (argsOk,error) = CheckArgs(args);
            if (!argsOk)
            {
                Console.WriteLine(error);
                Console.ReadKey();
                return;
            }
#endif
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(args[0]);

            
            new Thread(() =>
            {
                new PacketHandler().StartServer(packet);
            }).Start();
            new Thread(() =>
            {
                while (true)
                {
                    GameUpdate.Update();
                }
            }).Start();
        }

        private static (bool,string) CheckArgs(string[] args)
        {
            if (args.Length != argsCount)
            {
                return (false, "必要な引数がありません <コンフィグパスのディレクトリ>");
            }
            
            
            if (!Directory.Exists(args[0]))
            {
                return (false, $"{args[0]}のコンフィグパスのディレクトリが存在しません");
            }
            
            
            return (true, "");
        }
    }
}
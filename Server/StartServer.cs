using System;
using System.Threading;
using Core.Item;
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
        public static void Main(string[] args)
        {
            var (packet,serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            PacketHandler packetHandler = null;
            
            new Thread(() =>
            {
                packetHandler = new PacketHandler();
                packetHandler.StartServer(packet);
            }).Start();
            
            
            //ここはデバッグ用の仮コマンドコードです。今後きちんとしたコマンドのコードを別に記述します。
            while (true)
            {
                var command = Console.ReadLine()?.Split(" ");
                try
                {
                    if(command == null) continue;
                    
                    if (command[0] == "give")
                    {
                        var playerId = int.Parse(command[1]);
                        var slot = int.Parse(command[2]);
                        var itemId = int.Parse(command[3]);
                        var amount = int.Parse(command[4]);
                        
                        var playerInventory = serviceProvider.GetService<PlayerInventoryDataStore>()?.GetInventoryData(playerId);
                        playerInventory.SetItem(slot,ItemStackFactory.Create(itemId,amount));
                        Console.WriteLine("Gave item for player " + playerId);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
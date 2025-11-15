using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class SendCommandProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:sendCommand";
        
        public const string GiveCommand = "give";
        public const string ClearInventoryCommand = "clearInventory";
        public const string TrainAutoRunCommand = "trainAutoRun";
        //public const string TrainAutoRunOnArgument = "on";
        //public const string TrainAutoRunOffArgument = "off";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public SendCommandProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SendCommandProtocolMessagePack>(payload.ToArray());
            
            var command = data.Command.Split(' '); //command text
            
            //他のコマンドを実装する場合、この実装方法をやめる
            if (command[0] == GiveCommand)
            {
                var inventory = _playerInventoryDataStore.GetInventoryData(int.Parse(command[1]));
                
                var itemId = new ItemId(int.Parse(command[2]));
                var count = int.Parse(command[3]);
                
                var item = ServerContext.ItemStackFactory.Create(itemId, count);
                inventory.MainOpenableInventory.InsertItem(item);
            }
            else if (command[0] == ClearInventoryCommand)
            {
                var inventory = _playerInventoryDataStore.GetInventoryData(int.Parse(command[1]));
                for (var i = 0; i < inventory.MainOpenableInventory.InventoryItems.Count; i++)
                {
                    inventory.MainOpenableInventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
                }
            }
            else if (command[0] == TrainAutoRunCommand)
            {
                // トグル引数に応じて全列車の自動運転状態を決定
                // Decide auto-run state for every train based on the toggle argument
                TrainUpdateService.TurnOnorOffTrainAutoRun(command);
            }

            return null;
        }

        [MessagePackObject]
        public class SendCommandProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public string Command { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendCommandProtocolMessagePack()
            {
            }
            
            public SendCommandProtocolMessagePack(string command)
            {
                Tag = ProtocolTag;
                Command = command;
            }
        }

    }

}
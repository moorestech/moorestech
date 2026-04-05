using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SendCommandProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:sendCommand";
        
        public const string GiveCommand = "give";
        public const string ClearInventoryCommand = "clearInventory";
        public const string TrainAutoRunCommand = "trainAutoRun";
        public const string TrainAutoRunOnArgument = "on";
        public const string TrainAutoRunOffArgument = "off";
        public const string GetPlayTimeCommand = "getPlayTime";
        public const string AddFuelToAllTrainCarsCommand = "addFuelToAllTrainCarsCommand";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly TrainUpdateService _trainUpdateService;
        
        public SendCommandProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _worldSettingsDatastore = serviceProvider.GetService<IWorldSettingsDatastore>();
            _trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<SendCommandProtocolMessagePack>(payload);
            
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
                _trainUpdateService.TurnOnorOffTrainAutoRun(command);
            }
            else if (command[0] == GetPlayTimeCommand)
            {
                // 累積プレイ時間を取得してログ出力
                // Get total play time and output to log
                var playTime = _worldSettingsDatastore.GetCurrentPlayTime();
                Debug.Log($"[PlayTime] Total: {playTime.TotalHours:F2} hours ({playTime})");
            }
            else if (command[0] == AddFuelToAllTrainCarsCommand)
            {
                IEnumerable<TrainUnit> trainUnits = _trainUpdateService.GetRegisteredTrains();
                foreach (var trainUnit in trainUnits)
                {
                    foreach (var trainCar in trainUnit.Cars)
                    {
                        if (trainCar.TractionForce <= 0) continue;
                        if (trainCar.Container == null) trainCar.SetContainer(ItemTrainCarContainer.CreateWithEmptySlots(trainCar.TrainCarMasterElement.InventorySlots));
                        if (trainCar.Container is not ItemTrainCarContainer itemTrainCarContainer) continue;
                        
                        var emptySlotIndex = -1;
                        if (trainCar.TrainCarMasterElement.TrainFuelItems == null || trainCar.TrainCarMasterElement.TrainFuelItems.Length == 0) continue;
                        
                        var fuel = trainCar.TrainCarMasterElement.TrainFuelItems[0];
                        
                        for (var j = 0; j < itemTrainCarContainer.InventoryItems.Length; j++)
                        {
                            var stack = itemTrainCarContainer.InventoryItems[j].Stack;
                            if (stack.Id == ItemMaster.EmptyItemId) emptySlotIndex = j;
                        }
                        
                        if (emptySlotIndex == -1) continue;
                        
                        var item = ServerContext.ItemStackFactory.Create(fuel.ItemGuid, 10);
                        itemTrainCarContainer.SetItem(emptySlotIndex, item);
                        Debug.Log($"add fuel to train car {MasterHolder.ItemMaster.GetItemMaster(fuel.ItemGuid).Name} x 10");
                    }
                }
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

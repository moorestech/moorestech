using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.DataStore;
using Game.Train.Unit;
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
        public const string GearNetworkInfoCommand = "gearNetworkInfo";

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
            else if (command[0] == GearNetworkInfoCommand)
            {
                // 全歯車ネットワークの情報をログ出力
                // Log all gear network info with belt conveyor power ratio
                LogGearNetworkInfo();
            }

            return null;
        }

        private void LogGearNetworkInfo()
        {
            var networks = GearNetworkDatastore.GetAllGearNetworks();
            var totalPowerAll = 0f;
            var totalBeltPowerAll = 0f;
            var totalBeltCountAll = 0;
            var networkIndex = 0;

            foreach (var (networkId, network) in networks)
            {
                var info = network.CurrentGearNetworkInfo;

                // CheckedGearComponentsからベルトコンベアの消費電力を集計（停止中NWでも正確）
                // Aggregate belt conveyor power from CheckedGearComponents (accurate even for stopped networks)
                var beltPower = 0f;
                var beltCount = 0;
                foreach (var (blockId, rotationInfo) in network.CheckedGearComponents)
                {
                    if (rotationInfo.EnergyTransformer is GearBeltConveyorComponent)
                    {
                        beltPower += rotationInfo.RequiredTorque.AsPrimitive() * rotationInfo.Rpm.AsPrimitive();
                        beltCount++;
                    }
                }

                var beltRatio = info.TotalRequiredGearPower > 0 ? beltPower / info.TotalRequiredGearPower * 100f : 0f;

                Debug.Log($"[GearNetwork #{networkIndex}] " +
                          $"Generators:{network.GearGenerators.Count} Transformers:{network.GearTransformers.Count} " +
                          $"Required:{info.TotalRequiredGearPower:F1}GP Generate:{info.TotalGenerateGearPower:F1}GP " +
                          $"OpRate:{info.OperatingRate:P0} Stop:{info.StopReason} " +
                          $"BeltConveyors:{beltCount} BeltPower:{beltPower:F1}GP ({beltRatio:F1}%)");

                totalPowerAll += info.TotalRequiredGearPower;
                totalBeltPowerAll += beltPower;
                totalBeltCountAll += beltCount;
                networkIndex++;
            }

            // 全ネットワークのサマリーをログ出力
            // Log summary across all networks
            var totalBeltRatio = totalPowerAll > 0 ? totalBeltPowerAll / totalPowerAll * 100f : 0f;
            Debug.Log($"[GearNetwork Summary] Networks:{networks.Count} " +
                      $"BeltConveyors:{totalBeltCountAll} " +
                      $"BeltPower:{totalBeltPowerAll:F1}/{totalPowerAll:F1}GP ({totalBeltRatio:F1}%)");
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

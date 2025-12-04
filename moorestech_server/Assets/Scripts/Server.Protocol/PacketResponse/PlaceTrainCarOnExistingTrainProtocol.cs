using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using Game.Train.Train;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnExistingTrainProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCarOnExistingTrain";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public PlaceTrainCarOnExistingTrainProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<PlaceTrainOnExistingTrainRequestMessagePack>(payload.ToArray());
            
            // 手持ちのアイテム取得
            // Get the item from player's inventory
            var mainInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;
            var item = mainInventory.GetItem(request.InventorySlot);
            
            // 列車ユニットを検索
            // Build train unit from composition data
            var trainUnit = GetTargetTrainUnit(request.TargetTrainCarId);
            if (trainUnit == null) return null;
            
            // TrainCarを作成
            if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(item.Id, out var trainCarMasterElement))
            {
                return null;
            }
            var trainCar = new TrainCar(
                trainCarMasterElement
            );
            var targetRailNode = RailConnectionEditProtocol.ResolveRailComponent(request.RailSpecifier);
            
            //TODO: trainUnitに生成したtrainCarを追加
            // trainUnit.AddTrain(trainCar, )
            Debug.LogWarning("TODO: trainUnitに生成したtrainCarを追加");
            
            // アイテムを消費
            // Consume the train item from inventory
            mainInventory.SetItem(request.InventorySlot, item.Id, item.Count - 1);
            
            return null;
        }
        
        #region Internal
        
        private TrainUnit GetTargetTrainUnit(Guid targetTrainId)
        {
            var targetTrainUnit = TrainUpdateService.Instance
                .GetRegisteredTrains()
                .First(u => u.Cars.FirstOrDefault(c => c.CarId == targetTrainId) != null);
            
            return targetTrainUnit;
        }
        
        #endregion
        
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class PlaceTrainOnExistingTrainRequestMessagePack : ProtocolMessagePackBase
        {
            public PlaceTrainOnExistingTrainRequestMessagePack()
            {
                Tag = ProtocolTag;
            }
            
            public PlaceTrainOnExistingTrainRequestMessagePack(
                int hotBarSlot,
                int playerId,
                Guid targetTrainCarId,
                RailConnectionEditProtocol.RailComponentSpecifier railSpecifier)
            {
                Tag = ProtocolTag;
                HotBarSlot = hotBarSlot;
                PlayerId = playerId;
                TargetTrainCarId = targetTrainCarId;
                RailSpecifier = railSpecifier;
            }
            [Key(2)] public int HotBarSlot { get; set; }
            [Key(3)] public int PlayerId { get; set; }
            [Key(4)] public Guid TargetTrainCarId { get; set; }
            [Key(5)] public RailConnectionEditProtocol.RailComponentSpecifier RailSpecifier { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
        }
        
        #endregion
    }
}
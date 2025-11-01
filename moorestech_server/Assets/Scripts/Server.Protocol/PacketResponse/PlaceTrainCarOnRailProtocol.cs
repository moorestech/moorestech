using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using RailComponentSpecifier = Server.Protocol.PacketResponse.RailConnectionEditProtocol.RailComponentSpecifier;
using Game.Block.Interface.Extension;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnRailProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCar";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlaceTrainCarOnRailProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<PlaceTrainOnRailRequestMessagePack>(payload.ToArray());

            // リクエスト内容を検証し、列車配置を実行
            // Validate request contents and execute train placement
            
            var railComponent = RailConnectionEditProtocol.ResolveRailComponent(request.RailSpecifier);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(request.PlayerId);
            if (railComponent == null || inventoryData == null) return null;
            
            var mainInventory = inventoryData.MainOpenableInventory;
            
            // ホットバースロットからアイテムを取得
            // Get item from hotbar slot
            var item = mainInventory.GetItem(request.InventorySlot);
            if (item.Id == ItemMaster.EmptyItemId || item.Count == 0) return null;
            
            // 列車ユニット生成
            // Build train unit from composition data
            CreateTrainUnit(railComponent, item.Id);
            
            // アイテムを消費
            // Consume the train item from inventory
            mainInventory.SetItem(request.InventorySlot, item.Id, item.Count - 1);
            
            return null;
            
            #region Internal
            
            TrainUnit CreateTrainUnit(RailComponent railComponent, ItemId trainItemId)
            {
                // TODO MasterHolderのTrainUnitMasterから列車データを取得してTrainCarを生成する処理を実装する
                
                var railNodes = new List<RailNode>
                {
                    railComponent.FrontNode,
                    railComponent.BackNode
                };
                
                var railPosition = new RailPosition(railNodes,
                return new TrainUnit(railPosition
            }
            
            #endregion
        }

        #region MessagePack Classes

        [MessagePackObject]
        public class PlaceTrainOnRailRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailComponentSpecifier RailSpecifier { get; set; }
            [Key(3)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            [Key(4)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }

            public PlaceTrainOnRailRequestMessagePack(
                RailComponentSpecifier railSpecifier,
                int hotBarSlot,
                int playerId)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailSpecifier = railSpecifier;
                HotBarSlot = hotBarSlot;
                PlayerId = playerId;
            }
        }

        #endregion
    }
}


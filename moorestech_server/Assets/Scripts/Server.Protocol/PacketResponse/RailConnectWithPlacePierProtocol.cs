using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectWithPlacePierProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectWithPlacePier";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public RailConnectWithPlacePierProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RailConnectWithPlacePierRequest>(payload.ToArray());
            var inventoryData = _playerInventoryDataStore.GetInventoryData(request.PlayerId);
            
            // すでにブロックがある場合はそのまま処理を終了
            if (ServerContext.WorldBlockDatastore.Exists(request.PierPlaceInfo.Position)) return null;
            
            // アイテムIDがブロックIDに変換できない場合はそのまま処理を終了
            var item = inventoryData.MainOpenableInventory.GetItem(request.PierInventorySlot);
            if (!MasterHolder.BlockMaster.IsBlock(item.Id)) return null;
            
            // ブロックIDの設定
            var blockId = MasterHolder.BlockMaster.GetBlockId(item.Id);
            blockId = blockId.GetVerticalOverrideBlockId(request.PierPlaceInfo.VerticalDirection);
            
            // paramsの設定
            BlockCreateParam[] createParams = request.PierPlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            
            // ブロックの設置
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, request.PierPlaceInfo.Position, request.PierPlaceInfo.Direction, createParams, out var block);
            
            // アイテムを減らし、セットする
            item = item.SubItem(1);
            inventoryData.MainOpenableInventory.SetItem(request.PierInventorySlot, item);
            
            return null;
        }
        
        [MessagePackObject]
        public class RailConnectWithPlacePierRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack PierPlaceInfo
            {
                get;
                set;
            }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int PierInventorySlot { get; set; }
            [Key(7)] public Guid RailTypeGuid { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectWithPlacePierRequest()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }
            
            public static RailConnectWithPlacePierRequest Create(int playerId, int fromNodeId, Guid fromGuid, int pierInventorySlot, PlaceInfo placeInfo, Guid railTypeGuid)
            {
                return new RailConnectWithPlacePierRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    PierInventorySlot = pierInventorySlot,
                    RailTypeGuid = railTypeGuid,
                    PierPlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(placeInfo),
                };
            }
        }
    }
}
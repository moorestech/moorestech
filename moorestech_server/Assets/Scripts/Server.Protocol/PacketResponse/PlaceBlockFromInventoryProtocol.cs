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
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlaceBlockFromInventoryProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeBlock";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public PlaceBlockFromInventoryProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceBlockFromInventoryMessagePack>(payload.ToArray());
            var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            
            foreach (var placeInfo in data.PlaceInfos)
            {
                PlaceBlock(placeInfo, data, inventoryData);
            }
            
            return null;
        }
        
        private static void PlaceBlock(PlaceInfoMessagePack placeInfo, SendPlaceBlockFromInventoryMessagePack data, PlayerInventoryData inventoryData)
        {
            //すでにブロックがある場合はそもまま処理を終了
            if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;
            
            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            var item = inventoryData.MainOpenableInventory.GetItem(data.InventorySlot);
            if (!MasterHolder.BlockMaster.IsBlock(item.Id)) return;
            
            // ブロックIDの設定
            var blockId = MasterHolder.BlockMaster.GetBlockId(item.Id);
            blockId = blockId.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);
            
            // paramsの作成
            BlockCreateParam[] createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            
            //ブロックの設置
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placeInfo.Position, placeInfo.Direction, createParams, out var block);
            
            //アイテムを減らし、セットする
            item = item.SubItem(1);
            inventoryData.MainOpenableInventory.SetItem(data.InventorySlot, item);
        }
        
        [MessagePackObject]
        public class SendPlaceBlockFromInventoryMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int InventorySlot { get; set; }
            [Key(4)] public List<PlaceInfoMessagePack> PlaceInfos { get; set; }
            
            public SendPlaceBlockFromInventoryMessagePack(int playerId, int inventorySlot, List<PlaceInfo> placeInfos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                InventorySlot = inventorySlot;
                PlaceInfos = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
            }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceBlockFromInventoryMessagePack() { }
        }
        
        [MessagePackObject]
        public class PlaceInfoMessagePack
        {
            [Key(0)] public Vector3IntMessagePack Position { get; set; }
            
            [Key(1)] public BlockDirection Direction { get; set; }
            
            [Key(2)] public BlockVerticalDirection VerticalDirection { get; set; }
            [Key(3)] public PlaceBlockFromHotBarProtocol.BlockCreateParamMessagePack[] BlockCreateParams { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceInfoMessagePack() { }
            
            public PlaceInfoMessagePack(PlaceInfo placeInfo)
            {
                BlockCreateParams = placeInfo.CreateParams.Select(v => new PlaceBlockFromHotBarProtocol.BlockCreateParamMessagePack(v)).ToArray();
                Position = new Vector3IntMessagePack(placeInfo.Position);
                Direction = placeInfo.Direction;
                VerticalDirection = placeInfo.VerticalDirection;
            }
        }
    }
}
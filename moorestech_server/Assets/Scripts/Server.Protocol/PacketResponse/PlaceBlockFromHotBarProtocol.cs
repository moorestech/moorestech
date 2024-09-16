using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SendPlaceHotBarBlockProtocol : IPacketResponse
    {
        public const string Tag = "va:palceHotbarBlock";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public SendPlaceHotBarBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceHotBarBlockProtocolMessagePack>(payload.ToArray());
            var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            
            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo, data, inventoryData);
            }
            
            return null;
        }
        
        #region GetResponse
        
        static void PlaceBlock(PlaceInfoMessagePack placeInfo,SendPlaceHotBarBlockProtocolMessagePack data,PlayerInventoryData inventoryData)
        {
            //すでにブロックがある場合はそもまま処理を終了
            if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;
            
            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            var item = inventoryData.MainOpenableInventory.GetItem(data.InventorySlot);
            if (!MasterHolder.BlockMaster.IsBlock(item.Id)) return;
            
            // ブロックIDの設定
            var blockId = MasterHolder.BlockMaster.GetBlockId(item.Id);
            blockId = GetOverrideBlockId(blockId, placeInfo.VerticalDirection);
            
            //ブロックの設置
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placeInfo.Position, placeInfo.Direction, out var block);
            
            //アイテムを減らし、セットする
            item = item.SubItem(1);
            inventoryData.MainOpenableInventory.SetItem(data.InventorySlot, item);
        }
        
        static BlockId GetOverrideBlockId(BlockId blockId,BlockVerticalDirection verticalDirection)
        {
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var overrideBlock = blockElement.OverrideVerticalBlock;
            
            if (verticalDirection is BlockVerticalDirection.Up && overrideBlock.UpBlockGuid != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(overrideBlock.UpBlockGuid);
            }
            if (verticalDirection is BlockVerticalDirection.Horizontal && overrideBlock.HorizontalBlockGuid != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(overrideBlock.HorizontalBlockGuid);
            }
            if (verticalDirection is  BlockVerticalDirection.Down && overrideBlock.DownBlockGuid != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(overrideBlock.DownBlockGuid);
            }
            
            return blockId;
        }
        
        #endregion
    }
    
    public class PlaceInfo
    {
        public Vector3Int Position { get; set; }
        public BlockDirection Direction { get; set; }
        public BlockVerticalDirection VerticalDirection { get; set; }
        
        public bool Placeable { get; set; }
    }
    
    
    [MessagePackObject]
    public class SendPlaceHotBarBlockProtocolMessagePack : ProtocolMessagePackBase
    {
        public SendPlaceHotBarBlockProtocolMessagePack(int playerId, int hotBarSlot, List<PlaceInfo> placeInfos)
        {
            Tag = SendPlaceHotBarBlockProtocol.Tag;
            PlayerId = playerId;
            HotBarSlot = hotBarSlot;
            PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SendPlaceHotBarBlockProtocolMessagePack()
        {
        }
        
        [Key(2)] public int PlayerId { get; set; }
        
        [Key(3)] public int HotBarSlot { get; set; }
        [IgnoreMember] public int InventorySlot =>  PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
        
        [Key(4)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }
    }
    
    [MessagePackObject]
    public class PlaceInfoMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        
        [Key(1)] public BlockDirection Direction { get; set; }
        
        [Key(2)] public BlockVerticalDirection VerticalDirection { get; set; }
        
        public PlaceInfoMessagePack()
        {
        }
        
        public PlaceInfoMessagePack(PlaceInfo placeInfo)
        {
            Position = new Vector3IntMessagePack(placeInfo.Position);
            Direction = placeInfo.Direction;
            VerticalDirection = placeInfo.VerticalDirection;
        }
    }
}
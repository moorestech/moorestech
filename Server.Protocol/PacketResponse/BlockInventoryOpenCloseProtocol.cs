using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.Base;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryOpenCloseProtocol : IPacketResponse
    {
        public const string Tag = "va:blockInvOpen";
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenState;

        public BlockInventoryOpenCloseProtocol(ServiceProvider serviceProvider)
        {
            _inventoryOpenState = serviceProvider.GetService<IBlockInventoryOpenStateDataStore>();
        }

        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<BlockInventoryOpenCloseProtocolMessagePack>(payload.ToArray());
            
            //開く、閉じるのセット
            if (data.IsOpen)
            {
                _inventoryOpenState.Open(data.PlayerId,data.X,data.Y);
            }
            else
            {
                _inventoryOpenState.Close(data.PlayerId);
            }
            
            return new List<ToClientProtocolMessagePackBase>();
        }
        
    }
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class BlockInventoryOpenCloseProtocolMessagePack : ToServerProtocolMessagePackBase 
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockInventoryOpenCloseProtocolMessagePack() { }

        public BlockInventoryOpenCloseProtocolMessagePack(int playerId, int x, int y, bool isOpen)
        {
            ToServerTag = BlockInventoryOpenCloseProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
            IsOpen = isOpen;
        }

        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
}
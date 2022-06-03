using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
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

        public List<List<byte>> GetResponse(List<byte> payload)
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
            
            return new List<List<byte>>();
        }
        
    }
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class BlockInventoryOpenCloseProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
}
using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

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

            
            if (data.IsOpen)
                _inventoryOpenState.Open(data.PlayerId, data.X, data.Y);
            else
                _inventoryOpenState.Close(data.PlayerId);

            return new List<List<byte>>();
        }
    }


    [MessagePackObject(true)]
    public class BlockInventoryOpenCloseProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public BlockInventoryOpenCloseProtocolMessagePack()
        {
        }

        public BlockInventoryOpenCloseProtocolMessagePack(int playerId, int x, int y, bool isOpen)
        {
            Tag = BlockInventoryOpenCloseProtocol.Tag;
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
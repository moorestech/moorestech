using System;
using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class CraftProtocol : IPacketResponse
    {
        public const string Tag = "va:craft";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public CraftProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort(); // Packet ID
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var craftType = byteListEnumerator.MoveNextToGetByte();

            var craftingInventory = _playerInventoryDataStore.GetInventoryData(playerId).CraftingOpenableInventory;
            
            
            //クラフトの実行
            switch (craftType)
            {
                case 0:
                    craftingInventory.NormalCraft();
                    break;
                case 1:
                    craftingInventory.AllCraft();
                    break;
                case 2:
                    craftingInventory.OneStackCraft();
                    break;
            }
            

            return new List<List<byte>>();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class CraftProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
}
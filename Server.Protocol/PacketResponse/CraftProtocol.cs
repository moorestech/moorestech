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
            var data = MessagePackSerializer.Deserialize<CraftProtocolMessagePack>(payload.ToArray());
            
            var craftingInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).CraftingOpenableInventory;
            
            
            //クラフトの実行
            switch (data.CraftType)
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
        public CraftProtocolMessagePack(int playerId, int craftType)
        {
            PlayerId = playerId;
            CraftType = craftType;
            Tag = CraftProtocol.Tag;
        }

        /// <summary>
        /// デシリアライズ用のコンストラクタです
        /// 基本的に使用しないでください
        /// </summary>
        public CraftProtocolMessagePack() { }

        public int PlayerId { get; set; }
        public int CraftType { get; set; }
    }
}
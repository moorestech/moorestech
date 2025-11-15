using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.CraftChainer.BlockComponent.Computer;
using Game.CraftChainer.BlockComponent.Crafter;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Game.Common.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetCraftChainerMainComputerRequestItemProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setReuqestComputer";
        
        public SetCraftChainerMainComputerRequestItemProtocol(ServiceProvider serviceProvider) { }
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SetCraftChainerMainComputerRequestItemProtocolMessagePack>(payload.ToArray());
            
            var blockPos = data.BlockPos.Vector3Int;
            
            var crafterBlock = ServerContext.WorldBlockDatastore.GetBlock(blockPos);
            if (crafterBlock == null) return null;
            
            var itemId = data.ItemId;
            var count = data.Count;
            var computerComponent = crafterBlock.ComponentManager.GetComponent<CraftChainerMainComputerComponent>();
            computerComponent.StartCreateItem(itemId, count);
            
            return null;
        }
        
        [MessagePackObject]
        public class SetCraftChainerMainComputerRequestItemProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack BlockPos { get; set; }
            [Key(3)] public int ItemIdInt { get; set; }
            [IgnoreMember] public ItemId ItemId => new(ItemIdInt);
            [Key(4)] public int Count { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SetCraftChainerMainComputerRequestItemProtocolMessagePack() { }
            
            public SetCraftChainerMainComputerRequestItemProtocolMessagePack(Vector3Int blockPos, ItemId itemId, int count)
            {
                Tag = ProtocolTag;
                BlockPos = new Vector3IntMessagePack(blockPos);
                ItemIdInt = itemId.AsPrimitive();
                Count = count;
            }
        }
    }
}

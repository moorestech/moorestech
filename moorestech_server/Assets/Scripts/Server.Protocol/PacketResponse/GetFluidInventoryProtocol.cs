using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Context;
using Game.Fluid;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class GetFluidInventoryProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getFluidInv";
        
        public GetFluidInventoryProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryRequestMessagePack>(payload.ToArray());
            
            // IFluidInventoryコンポーネントを持つかチェック
            var blockDatastore = ServerContext.WorldBlockDatastore;
            if (!blockDatastore.ExistsComponent<IFluidInventory>(data.Pos))
                return new GetFluidInventoryResponseMessagePack(data.Pos, new FluidMessagePack[0]);
            
            // 流体インベントリを取得
            var fluidInventory = blockDatastore.GetBlock<IFluidInventory>(data.Pos);
            var fluidStacks = fluidInventory.GetFluidInventory();
            
            // FluidStackをMessagePack形式に変換
            var fluidMessages = fluidStacks.Select(stack => new FluidMessagePack(stack.FluidId, stack.Amount)).ToArray();
            
            return new GetFluidInventoryResponseMessagePack(data.Pos, fluidMessages);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class GetFluidInventoryRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Pos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetFluidInventoryRequestMessagePack()
            {
            }
            
            public GetFluidInventoryRequestMessagePack(Vector3Int pos)
            {
                Tag = ProtocolTag;
                Pos = new Vector3IntMessagePack(pos);
            }
        }
        
        [MessagePackObject]
        public class GetFluidInventoryResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Pos { get; set; }
            [Key(3)] public FluidMessagePack[] Fluids { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetFluidInventoryResponseMessagePack()
            {
            }
            
            public GetFluidInventoryResponseMessagePack(Vector3Int pos, FluidMessagePack[] fluids)
            {
                Tag = ProtocolTag;
                Pos = new Vector3IntMessagePack(pos);
                Fluids = fluids;
            }
        }
        
        [MessagePackObject]
        public class FluidMessagePack
        {
            [Key(0)] public int FluidId { get; set; }
            [Key(1)] public double Amount { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public FluidMessagePack()
            {
            }
            
            public FluidMessagePack(FluidId fluidId, double amount)
            {
                FluidId = (int)fluidId;
                Amount = amount;
            }
        }
        
        #endregion
    }
}
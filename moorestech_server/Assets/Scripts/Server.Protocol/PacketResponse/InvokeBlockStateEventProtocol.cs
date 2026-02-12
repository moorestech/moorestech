using System;
using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class InvokeBlockStateEventProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invokeBlockState";
        
        private readonly ChangeBlockStateEventPacket _changeBlockStateEventPacket;
        
        public InvokeBlockStateEventProtocol(ServiceProvider serviceProvider)
        {
            _changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RequestInvokeBlockStateProtocolMessagePack>(payload.ToArray());
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            Vector3Int position = request.Position;
            
            // 指定座標のブロックを取得
            var blockData = worldBlockDatastore.GetOriginPosBlock(position);
            
            // ブロックの状態を取得
            var blockState = blockData?.Block.GetBlockState();
            if (blockState == null) return null;
            
            // イベントを発行
            _changeBlockStateEventPacket.ChangeState((blockState, blockData));
            
            return null;
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class RequestInvokeBlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            
            [Obsolete("This constructor is for deserialization. Do not use directly.")]
            public RequestInvokeBlockStateProtocolMessagePack()
            {
            }
            
            public RequestInvokeBlockStateProtocolMessagePack(Vector3Int position)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
            }
        }
        
        #endregion
    }
}
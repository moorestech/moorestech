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
    // ブロックのステートをリクエストする。返答は直接返すのではなく、通常と同様のイベントを経由して返す
    public class RequestBlockStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:requestBlockState";
        
        private readonly ChangeBlockStateEventPacket _changeBlockStateEventPacket;
        
        public RequestBlockStateProtocol(ServiceProvider serviceProvider)
        {
            _changeBlockStateEventPacket = serviceProvider.GetService<ChangeBlockStateEventPacket>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<RequestBlockStateProtocolMessagePack>(payload);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            Vector3Int position = request.Position;
            
            // 指定座標のブロックを取得
            var blockData = worldBlockDatastore.GetOriginPosBlock(position);
            
            // ブロックの状態を取得
            var blockState = blockData?.Block.GetBlockState();
            if (blockState == null) return null;
            
            // 初期pullのため差分スキップ禁止で発行
            // Fire without diff-skip for the initial pull
            _changeBlockStateEventPacket.ForceChangeState((blockState, blockData));
            
            return null;
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class RequestBlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestBlockStateProtocolMessagePack()
            {
            }
            
            public RequestBlockStateProtocolMessagePack(Vector3Int position)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
            }
        }
        
        #endregion
    }
}
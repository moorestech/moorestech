using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryOpenCloseProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:blockInvOpen";
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenState;
        
        public BlockInventoryOpenCloseProtocol(ServiceProvider serviceProvider)
        {
            _inventoryOpenState = serviceProvider.GetService<IBlockInventoryOpenStateDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<BlockInventoryOpenCloseProtocolMessagePack>(payload.ToArray());
            
            //開く、閉じるのセット
            if (data.IsOpen)
                _inventoryOpenState.Open(data.PlayerId, data.Pos);
            else
                _inventoryOpenState.Close(data.PlayerId);
            
            return null;
        }
        
        
        [MessagePackObject]
        public class BlockInventoryOpenCloseProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            
            [Key(3)] public Vector3IntMessagePack Pos { get; set; }
            
            [Key(4)] public bool IsOpen { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BlockInventoryOpenCloseProtocolMessagePack() { }
            /// <summary>
            ///     TODO このプロトコル消していいのでは（どうせステートの変化を送るなら、それと一緒にインベントリの情報を送った方が設計的に楽なのでは？
            /// </summary>
            /// <param name="playerId"></param>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="isOpen"></param>
            public BlockInventoryOpenCloseProtocolMessagePack(int playerId, Vector3Int pos, bool isOpen)
            {
                Tag = ProtocolTag;
                Pos = new Vector3IntMessagePack(pos);
                PlayerId = playerId;
                IsOpen = isOpen;
            }
        }
    }
}
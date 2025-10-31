using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.BaseCamp;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class CompleteBaseCampProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:completeBaseCamp";
        
        public CompleteBaseCampProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<CompleteBaseCampProtocolMessagePack>(payload.ToArray());
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 指定位置のブロックを取得
            // Get block at specified position
            var block = worldBlockDatastore.GetBlock(data.Position);
            if (block == null) return null;
            
            // ベースキャンプコンポーネントを取得
            // Get base camp component
            var baseCampComponent = block.GetComponent<BaseCampComponent>();
            if (baseCampComponent == null) return null;
            
            // 完了していることを確認
            // Verify completion
            if (!baseCampComponent.IsCompleted()) return null;
            
            // 変換先のブロックIDを取得（BaseCampBlockParamから取得）
            // Get transformed block ID from BaseCampBlockParam
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
            var baseCampParam = blockMaster.BlockParam as BaseCampBlockParam;
            if (baseCampParam == null || baseCampParam.UpgradBlockGuid == System.Guid.Empty) return null;
            
            var transformedBlockId = MasterHolder.BlockMaster.GetBlockId(baseCampParam.UpgradBlockGuid);
            
            // ブロックを削除して新しいブロックを配置
            // Remove block and place new block
            var blockPositionInfo = block.BlockPositionInfo;
            worldBlockDatastore.RemoveBlock(data.Position);
            worldBlockDatastore.TryAddBlock(transformedBlockId, data.Position, blockPositionInfo.BlockDirection, Array.Empty<BlockCreateParam>(), out _);
            
            return null;
        }
        
        [MessagePackObject]
        public class CompleteBaseCampProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public Vector3IntMessagePack Position { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public CompleteBaseCampProtocolMessagePack() { }
            
            public CompleteBaseCampProtocolMessagePack(int playerId, Vector3Int position)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Position = new Vector3IntMessagePack(position);
            }
        }
    }
}

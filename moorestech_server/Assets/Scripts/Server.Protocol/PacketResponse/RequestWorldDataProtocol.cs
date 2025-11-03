using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Entity.Interface;
using Game.Train.Common;
using Game.Train.Entity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Util;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class RequestWorldDataProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getWorldData";
        private readonly IEntityFactory _entityFactory;
        
        public RequestWorldDataProtocol(ServiceProvider serviceProvider)
        {
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var blockMasterDictionary = ServerContext.WorldBlockDatastore.BlockMasterDictionary;
            var blockResult = new List<BlockDataMessagePack>();
            foreach (var blockMaster in blockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;
                var blockDirection = blockMaster.Value.BlockPositionInfo.BlockDirection;
                blockResult.Add(new BlockDataMessagePack(block.BlockId, pos, blockDirection));
            }
            
            // エンティティ収集：ベルトコンベアアイテムと列車
            // Collect entities: belt conveyor items and trains
            var entities = new List<EntityMessagePack>();
            
            // ベルトコンベアアイテムを収集
            // Collect belt conveyor items
            var items = CollectBeltConveyorItems.CollectItemFromWorld(_entityFactory);
            entities.AddRange(items.Select(item => new EntityMessagePack(item)));
            
            // 列車エンティティを収集
            // Collect train entities
            var trains = CollectTrainEntities();
            entities.AddRange(trains);
            
            return new ResponseWorldDataMessagePack(blockResult.ToArray(), entities.ToArray());
        }
        
        #region Internal
        
        /// <summary>
        /// 登録された全列車をTrainEntityに変換してEntityMessagePackとして返す
        /// Convert all registered trains to TrainEntity and return as EntityMessagePack
        /// </summary>
        private List<EntityMessagePack> CollectTrainEntities()
        {
            var trainEntities = new List<EntityMessagePack>();
            var registeredTrains = TrainUpdateService.Instance.GetRegisteredTrains();
            
            foreach (var trainUnit in registeredTrains)
            {
                if (trainUnit == null) continue;
                
                // TrainIdからEntityInstanceIdを生成
                // Generate EntityInstanceId from TrainId
                var instanceId = GenerateEntityInstanceIdFromGuid(trainUnit.TrainId);
                
                // TrainEntityを生成してEntityMessagePackに変換
                // Create TrainEntity and convert to EntityMessagePack
                var trainEntity = new TrainEntity(instanceId, trainUnit);
                trainEntities.Add(new EntityMessagePack(trainEntity));
            }
            
            return trainEntities;
        }
        
        /// <summary>
        /// GuidからEntityInstanceIdを生成する
        /// GuidのGetHashCode()を使用してlong値に変換
        /// Generate EntityInstanceId from Guid
        /// Convert to long value using Guid.GetHashCode()
        /// </summary>
        private EntityInstanceId GenerateEntityInstanceIdFromGuid(Guid guid)
        {
            // GuidのHashCodeからlong値を生成
            // Generate long value from Guid's HashCode
            var hashCode = guid.GetHashCode();
            return new EntityInstanceId(hashCode);
        }
        
        #endregion
        
        
        [MessagePackObject]
        public class RequestWorldDataMessagePack : ProtocolMessagePackBase
        {
            public RequestWorldDataMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseWorldDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockDataMessagePack[] Blocks { get; set; }
            [Key(3)] public EntityMessagePack[] Entities { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldDataMessagePack() { }
            public ResponseWorldDataMessagePack(BlockDataMessagePack[] Block, EntityMessagePack[] entities)
            {
                Tag = ProtocolTag;
                Blocks = Block;
                Entities = entities;
            }
        }
    }
}
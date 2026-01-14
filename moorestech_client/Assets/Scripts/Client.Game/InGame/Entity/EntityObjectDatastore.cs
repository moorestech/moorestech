using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Entity.Factory;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Client.Game.InGame.Train;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Entity
{
    public class EntityObjectDatastore : MonoBehaviour
    {
        private EntityObjectFactory _entityObjectFactory;
        private readonly Dictionary<long, (DateTime lastUpdate, IEntityObject objectEntity)> _entities = new();
        
        [Inject]
        public void Construct(TrainUnitClientCache trainUnitClientCache)
        {
            // 依存注入とファクトリー初期化
            // Dependency injection and factory initialization
            _entityObjectFactory = new EntityObjectFactory(trainUnitClientCache);
        }
        
        /// <summary>
        ///     エンティティ最終更新時間をチェックし、一定時間経過していたら削除する
        /// </summary>
        private void Update()
        {
            //1秒以上経過していたら削除
            var removeEntities = new List<long>();
            foreach (var entity in _entities)
                if (((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 1) && (entity.Value.objectEntity.DestroyFlagIfNoUpdate))
                    removeEntities.Add(entity.Key);
            foreach (var removeEntity in removeEntities)
            {
                _entities[removeEntity].objectEntity.Destroy();
                _entities.Remove(removeEntity);
            }
        }
        
        /// <summary>
        ///     エンティティの生成、更新を行う
        /// </summary>
        public void OnEntitiesUpdate(List<EntityResponse> entities)
        {
            foreach (var entity in entities)
            {
                // 既存エンティティの更新
                // Update existing entity
                if (_entities.ContainsKey(entity.InstanceId))
                {
                    var objectEntity = _entities[entity.InstanceId].objectEntity;
                    objectEntity.SetPositionWithLerp(entity.Position);
                    objectEntity.SetEntityData(entity.EntityData);
                    _entities[entity.InstanceId] = (DateTime.Now, objectEntity);
                    continue;
                }
                
                // 新規エンティティの生成
                // Create new entity
                _entityObjectFactory.CreateEntity(transform, entity).ContinueWith(entityObject =>
                {
                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                    
                    return entityObject;
                });
            }
        }

        public void RemoveTrainEntitiesNotInSnapshot(IReadOnlyCollection<Guid> activeTrainCarIds)
        {
            // スナップショットに存在しない列車エンティティを抽出する
            // Collect train entities missing from the snapshot
            var removeIds = new List<long>();
            foreach (var entry in _entities)
            {
                var trainEntity = entry.Value.objectEntity as TrainCarEntityObject;
                if (trainEntity == null) continue;
                if (!activeTrainCarIds.Contains(trainEntity.TrainCarId)) removeIds.Add(entry.Key);
            }

            // 不要な列車エンティティを破棄して辞書から除去する
            // Destroy and remove stale train entities
            for (var i = 0; i < removeIds.Count; i++)
            {
                var entityId = removeIds[i];
                _entities[entityId].objectEntity.Destroy();
                _entities.Remove(entityId);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity.Factory;
using Client.Network.API;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using Client.Game.InGame.Train.Unit;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Entity
{
    public class EntityObjectDatastore : MonoBehaviour, ISkitEntityObjectControl
    {
        private EntityObjectFactory _entityObjectFactory;
        private readonly Dictionary<long, (DateTime lastUpdate, IEntityObject objectEntity)> _entities = new();
        private readonly HashSet<long> _creatingEntityIds = new();
        
        [Inject]
        public void Construct(TrainUnitClientCache trainUnitClientCache)
        {
            // 依存注入とファクトリー初期化
            // Dependency injection and factory initialization
            _entityObjectFactory = new EntityObjectFactory();
        }
        
        /// <summary>
        ///     エンティティ最終更新時間をチェックし、一定時間経過していたら削除する
        /// </summary>
        private void Update()
        {
            // 一定時間更新が無いエンティティを破棄する
            // Destroy entities that have not been updated recently
            var removeEntities = new List<long>();
            foreach (var entity in _entities)
                if ((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 1)
                    removeEntities.Add(entity.Key);
            foreach (var removeEntity in removeEntities)
            {
                _entities[removeEntity].objectEntity.Destroy();
                _entities.Remove(removeEntity);
            }
        }

        // 非表示中はUpdateが止まり期限切れ破棄も止まるが、再表示直後のUpdateがまとめて回収する
        // While hidden, Update and its expiry sweep halt, but the first Update after re-showing collects them all
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
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
                if (_creatingEntityIds.Contains(entity.InstanceId)) continue;
                _creatingEntityIds.Add(entity.InstanceId);
                _entityObjectFactory.CreateEntity(transform, entity).ContinueWith(entityObject =>
                {
                    // 生成完了前に同じIDの更新が来ても二重登録しない
                    // Avoid duplicate registration when updates arrive before creation completes
                    _creatingEntityIds.Remove(entity.InstanceId);
                    if (_entities.ContainsKey(entity.InstanceId))
                    {
                        entityObject.Destroy();
                        return entityObject;
                    }

                    entityObject.Initialize(entity.InstanceId);
                    _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
                    return entityObject;
                });
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Entity.Interface;

namespace Game.Entity
{
    public class EntitiesDatastore : IEntitiesDatastore
    {

        private readonly IEntityFactory _entityFactory;
        
        //todo セーブとロードを実装する
        private readonly Dictionary<long,IEntity> _entities = new();

        public EntitiesDatastore(IEntityFactory entityFactory)
        {
            _entityFactory = entityFactory;
        }

        public void Add(IEntity entity)
        {
            _entities.Add(entity.InstanceId, entity);
        }

        public bool Exists(long instanceId)
        {
            return _entities.ContainsKey(instanceId);
        }

        public IEntity Get(long instanceId)
        {
            return _entities[instanceId];
        }

        public void SetPosition(long instanceId, ServerVector3 position)
        {
            if (_entities.TryGetValue(instanceId, out var entity))
            {
                entity.SetPosition(position);
                return;
            }
            throw new Exception("Entity not found " + instanceId);
        }

        public ServerVector3 GetPosition(long instanceId)
        {
            if (_entities.TryGetValue(instanceId, out var entity))
            {
                return entity.Position;
            }
            throw new Exception("Entity not found " + instanceId);
        }

        public List<SaveEntityData> GetSaveBlockDataList()
        {
            var saveData = new List<SaveEntityData>();
            foreach (var entity in _entities)
            {
                var e = entity.Value;
                saveData.Add(new SaveEntityData(e.EntityType,e.InstanceId,e.Position));
            }

            return saveData;
        }

        public void LoadBlockDataList(List<SaveEntityData> saveBlockDataList)
        {
            foreach (var save in saveBlockDataList)
            {
                var entity = _entityFactory.CreateEntity(save.Type,save.InstanceId);
                _entities.Add(entity.InstanceId,entity);

                var pos = new ServerVector3(save.X,save.Y,save.Z);
                SetPosition(save.InstanceId,pos);
            }
        }
    }
}
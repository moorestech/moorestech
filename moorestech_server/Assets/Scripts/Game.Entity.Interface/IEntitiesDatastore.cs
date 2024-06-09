using System.Collections.Generic;
using UnityEngine;

namespace Game.Entity.Interface
{
    public interface IEntitiesDatastore
    {
        public void Add(IEntity entity);
        public bool Exists(EntityInstanceId instanceId);
        public IEntity Get(EntityInstanceId instanceId);
        
        public void SetPosition(EntityInstanceId instanceId, Vector3 position);
        
        public Vector3 GetPosition(EntityInstanceId instanceId);
        
        
        public List<EntityJsonObject> GetSaveJsonObject();
        public void LoadBlockDataList(List<EntityJsonObject> saveBlockDataList);
    }
}
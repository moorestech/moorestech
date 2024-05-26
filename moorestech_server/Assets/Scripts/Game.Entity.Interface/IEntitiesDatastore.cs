using System.Collections.Generic;
using UnityEngine;

namespace Game.Entity.Interface
{
    public interface IEntitiesDatastore
    {
        public void Add(IEntity entity);
        public bool Exists(long instanceId);
        public IEntity Get(long instanceId);

        public void SetPosition(long instanceId, Vector3 position);

        public Vector3 GetPosition(long instanceId);


        public List<EntityJsonObject> GetSaveBlockDataList();
        public void LoadBlockDataList(List<EntityJsonObject> saveBlockDataList);
    }
}
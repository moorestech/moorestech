using System;
using Client.Common.Asset;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Entity.Interface;
using MessagePack;
using UnityEngine;

namespace Client.Game.InGame.Entity.Factory
{
    /// <summary>
    /// 列車エンティティを生成するファクトリー
    /// Factory to create train entity
    /// </summary>
    public class TrainEntityObjectFactory : IEntityObjectFactory
    {
        private const string AddressablePath = "Vanilla/Game/DefaultTrain";
        
        private readonly GameObject _defaultTrainPrefab;
        
        public TrainEntityObjectFactory()
        {
            _defaultTrainPrefab = AddressableLoader.LoadDefault<GameObject>(AddressablePath);
        }
        
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            var state = MessagePackSerializer.Deserialize<TrainEntityStateMessagePack>(entity.EntityData);
            
            if (MasterHolder.TrainUnitMaster.TryGetTrainUnit(state.TrainMasterId, out var trainCarMaster)) return CreateTrainEntity(entity.Position, _defaultTrainPrefab);
            
            var loadedPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainCarMaster.AddressablePath);
            if (loadedPrefab == null) return CreateTrainEntity(entity.Position, _defaultTrainPrefab);
            
            
            return CreateTrainEntity(entity.Position, loadedPrefab);
            
            #region Internal
            
            IEntityObject CreateTrainEntity(Vector3 position, GameObject prefab)
            {
                var trainObject = GameObject.Instantiate(prefab, position, Quaternion.identity, parent);
                
                var trainEntityObject = trainObject.AddComponent<TrainCarEntityObject>();
                trainEntityObject.SetTrain(state.TrainCarId, trainCarMaster);
                return trainEntityObject;
            }
            
            #endregion
        }
    }
}

using Client.Common.Asset;
using Client.Game.InGame.Entity.Object;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using Client.Game.InGame.Train;
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
        private readonly TrainUnitClientCache _trainUnitClientCache;
        
        public TrainEntityObjectFactory(TrainUnitClientCache trainUnitClientCache)
        {
            _defaultTrainPrefab = AddressableLoader.LoadDefault<GameObject>(AddressablePath);
            _trainUnitClientCache = trainUnitClientCache;
        }
        
        public async UniTask<IEntityObject> CreateEntity(Transform parent, EntityResponse entity)
        {
            var state = MessagePackSerializer.Deserialize<TrainEntityStateMessagePack>(entity.EntityData);
            if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(state.TrainMasterId, out var trainCarMaster)) return CreateTrainEntity(entity.Position, _defaultTrainPrefab);
            
            var loadedPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainCarMaster.AddressablePath);
            if (loadedPrefab == null) return CreateTrainEntity(entity.Position, _defaultTrainPrefab);
            
            return CreateTrainEntity(entity.Position, loadedPrefab);
            
            #region Internal
            
            IEntityObject CreateTrainEntity(Vector3 position, GameObject prefab)
            {
                var trainObject = GameObject.Instantiate(prefab, position, Quaternion.identity, parent);
                
                var trainEntityObject = trainObject.AddComponent<TrainCarEntityObject>();
                trainEntityObject.SetTrain(state.TrainCarId, trainCarMaster, _trainUnitClientCache);
                
                // TrainCarEntityChildrenObjectを付与
                foreach (var mesh in trainEntityObject.GetComponentsInChildren<MeshRenderer>())
                {
                    mesh.gameObject.AddComponent<TrainCarEntityChildrenObject>();
                    mesh.gameObject.AddComponent<MeshCollider>();
                }
                // 元からTrainCarEntityChildrenObjectがついているかもしれないので、再度取得して初期化する
                foreach (var trainCarEntityChildren in trainEntityObject.GetComponentsInChildren<TrainCarEntityChildrenObject>())
                {
                    trainCarEntityChildren.Initialize(trainEntityObject);
                }
                return trainEntityObject;
            }
            
            #endregion
        }
    }
}

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
            var trainUnitElement = FindTrainUnitByItemGuid();
            if (trainUnitElement == null) return CreateTrainEntity(entity.Position, _defaultTrainPrefab);
            
            
            var loadedPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainUnitElement.AddressablePath);
            if (loadedPrefab == null) return CreateTrainEntity(entity.Position, _defaultTrainPrefab);
            
            
            return CreateTrainEntity(entity.Position, loadedPrefab);
            
            #region Internal
            
            Mooresmaster.Model.TrainModule.TrainUnitMasterElement FindTrainUnitByItemGuid()
            {
                // Stateから列車IDを復元する
                // Restore train ID from state payload
                if (entity.State == null || entity.State.Length == 0) return null;
                var state = MessagePackSerializer.Deserialize<TrainEntityStateMessagePack>(entity.State);
                var trainId = state.TrainId;
                foreach (var unit in MasterHolder.TrainUnitMaster.Train.TrainUnits)
                {
                    // itemGuidとtrainIdの比較（簡易実装）
                    // Compare itemGuid with trainId (simplified implementation)
                    if (unit.ItemGuid.HasValue && unit.ItemGuid.Value == trainId)
                    {
                        return unit;
                    }
                }
                return null;
            }
            
            IEntityObject CreateTrainEntity(Vector3 position, GameObject prefab)
            {
                var trainObject = GameObject.Instantiate(prefab, position, Quaternion.identity, parent);
                
                var trainEntityObject = trainObject.AddComponent<TrainEntityObject>();
                return trainEntityObject;
            }
            
            #endregion
        }
    }
}

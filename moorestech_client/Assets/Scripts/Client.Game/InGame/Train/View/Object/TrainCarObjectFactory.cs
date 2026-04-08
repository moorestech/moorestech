using System;
using Client.Common.Asset;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    /// 蛻苓ｻ翫お繝ｳ繝・ぅ繝・ぅ繧堤函謌舌☆繧九ヵ繧｡繧ｯ繝医Μ繝ｼ
    /// Factory to create train entity
    /// </summary>
    public class TrainCarObjectFactory
    {
        private readonly TrainUnitClientCache _trainCache;

        public TrainCarObjectFactory(TrainUnitClientCache trainCache)
        {
            // 蟋ｿ蜍｢譖ｴ譁ｰ縺ｫ蠢・ｦ√↑萓晏ｭ倥ｒ菫晄戟縺吶ｋ
            // Hold dependencies required for pose updates
            _trainCache = trainCache;
        }

        public async UniTask<TrainCarEntityObject> CreateTrainCarObject(Transform parent, TrainCarSnapshot carSnapshot)
        {
            // 繧ｹ繝翫ャ繝励す繝ｧ繝・ヨ縺九ｉ繝槭せ繧ｿ繝ｼ繝・・繧ｿ繧貞叙蠕励☆繧・
            // Retrieve master data from snapshot
            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(carSnapshot.TrainCarMasterId, out var trainCarMasterElement))
            {
                throw new InvalidOperationException($"TrainCar master not found. TrainCarMasterId:{carSnapshot.TrainCarMasterId}");
            }

            // 謖・ｮ・Addressable 繧偵◎縺ｮ縺ｾ縺ｾ隱ｭ縺ｿ縲∝､ｱ謨玲凾縺ｯ萓句､悶↓縺吶ｋ
            // Load the requested Addressable directly and fail hard when it is missing
            var loadedPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainCarMasterElement.AddressablePath);
            if (loadedPrefab == null)
            {
                throw new InvalidOperationException($"TrainCar prefab load failed. AddressablePath:{trainCarMasterElement.AddressablePath}");
            }

            return CreateTrainEntity(loadedPrefab);

            #region Internal

            TrainCarEntityObject CreateTrainEntity(GameObject prefab)
            {
                // prefab を生成して列車 entity を構成する
                // Instantiate the prefab and build the train entity
                var trainObject = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
                var trainEntityObject = trainObject.AddComponent<TrainCarEntityObject>();
                trainEntityObject.SetTrain(carSnapshot.TrainCarInstanceId, trainCarMasterElement);

                // 子 renderer に削除対象と collider を追加する
                // Add delete targets and colliders to child renderers
                foreach (var mesh in trainEntityObject.GetComponentsInChildren<MeshRenderer>())
                {
                    mesh.gameObject.AddComponent<TrainCarEntityChildrenObject>();
                    mesh.gameObject.AddComponent<MeshCollider>();
                }

                // 既存分も含めて子オブジェクトを初期化する
                // Initialize child objects including already existing ones
                foreach (var trainCarEntityChildren in trainEntityObject.GetComponentsInChildren<TrainCarEntityChildrenObject>())
                {
                    trainCarEntityChildren.Initialize(trainEntityObject);
                }

                // 共通 context で表示 processor を駆動する updater を付与する
                // Attach the updater that drives view processors with a shared context
                var viewUpdater = trainObject.AddComponent<TrainCarViewUpdater>();
                viewUpdater.Initialize(trainEntityObject, _trainCache);
                return trainEntityObject;
            }

            #endregion
        }
    }
}

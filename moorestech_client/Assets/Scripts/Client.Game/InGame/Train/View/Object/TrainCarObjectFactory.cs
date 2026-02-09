using Client.Common.Asset;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.Unit;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    /// 列車エンティティを生成するファクトリー
    /// Factory to create train entity
    /// </summary>
    public class TrainCarObjectFactory
    {
        private const string AddressablePath = "Vanilla/Game/DefaultTrain";

        private readonly TrainUnitClientCache _trainCache;
        private readonly GameObject _defaultTrainPrefab;

        public TrainCarObjectFactory(TrainUnitClientCache trainCache)
        {
            // 姿勢更新に必要な依存を保持する
            // Hold dependencies required for pose updates
            _trainCache = trainCache;
            _defaultTrainPrefab = AddressableLoader.LoadDefault<GameObject>(AddressablePath);
        }

        public async UniTask<TrainCarEntityObject> CreateTrainCarObject(Transform parent, TrainCarSnapshot carSnapshot)
        {
            // スナップショットからマスターデータを取得する
            // Retrieve master data from snapshot
            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(carSnapshot.TrainCarMasterId, out var trainCarMasterElement)) return CreateTrainEntity(_defaultTrainPrefab);

            var loadedPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(trainCarMasterElement.AddressablePath);
            if (loadedPrefab == null) return CreateTrainEntity(_defaultTrainPrefab);

            return CreateTrainEntity(loadedPrefab);

            #region Internal

            TrainCarEntityObject CreateTrainEntity(GameObject prefab)
            {
                var trainObject = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);

                var trainEntityObject = trainObject.AddComponent<TrainCarEntityObject>();
                trainEntityObject.SetTrain(carSnapshot.TrainCarInstanceId, trainCarMasterElement);

                // 車両姿勢更新コンポーネントを関連付ける
                // Attach pose update component for this car
                var poseUpdater = trainObject.AddComponent<TrainCarEntityPoseUpdater>();
                poseUpdater.SetDependencies(trainEntityObject, _trainCache);

                // TrainCarEntityChildrenObjectを付与
                foreach (var mesh in trainEntityObject.GetComponentsInChildren<MeshRenderer>())
                {
                    mesh.gameObject.AddComponent<TrainCarEntityChildrenObject>();
                    mesh.gameObject.AddComponent<MeshCollider>();
                }
                // 元からTrainCarEntityChildrenObjectがついているかもしれないので、再度取得して初期化する
                // Re-fetch and initialize TrainCarEntityChildrenObject in case some already existed
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

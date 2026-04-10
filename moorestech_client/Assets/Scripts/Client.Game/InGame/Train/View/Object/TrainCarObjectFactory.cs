using System;
using Client.Common;
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
    /// 列車エンティティを生成するファクトリー
    /// Factory to create train entity
    /// </summary>
    public class TrainCarObjectFactory
    {
        private readonly TrainUnitClientCache _trainCache;

        public TrainCarObjectFactory(TrainUnitClientCache trainCache)
        {
            // 姿勢更新に必要な依存を保持する
            // Hold dependencies required for pose updates
            _trainCache = trainCache;
        }

        public async UniTask<TrainCarEntityObject> CreateTrainCarObject(Transform parent, TrainCarSnapshot carSnapshot)
        {
            // スナップショットからマスターデータを取得する
            // Retrieve master data from snapshot
            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(carSnapshot.TrainCarMasterId, out var trainCarMasterElement))
            {
                throw new InvalidOperationException($"TrainCar master not found. TrainCarMasterId:{carSnapshot.TrainCarMasterId}");
            }

            // 指定 Addressable をそのまま読み、失敗時は例外にする
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
                var trainObject = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
                SetBlockLayerRecursively(trainObject);

                var trainEntityObject = trainObject.AddComponent<TrainCarEntityObject>();
                trainEntityObject.SetTrain(carSnapshot.TrainCarInstanceId, trainCarMasterElement);

                // Animator を持つ車両には animation processor を補う
                // Ensure animated cars have a dedicated animation processor
                AttachAnimationProcessorIfNeeded(trainObject);

                // 車両姿勢更新コンポーネントを関連付ける
                // Attach pose update component for this car
                var poseUpdater = trainObject.AddComponent<TrainCarViewUpdater>();
                poseUpdater.Initialize(trainEntityObject, _trainCache);

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

                return trainEntityObject;
            }

            void SetBlockLayerRecursively(GameObject targetTrainObject)
            {
                // 削除レイ判定が命中するよう列車全体をBlockレイヤーへそろえる
                // Normalize the entire train hierarchy to the Block layer for delete raycasts
                foreach (var childTransform in targetTrainObject.GetComponentsInChildren<Transform>(true))
                {
                    childTransform.gameObject.layer = LayerConst.BlockLayer;
                }
            }

            void AttachAnimationProcessorIfNeeded(GameObject targetTrainObject)
            {
                if (targetTrainObject.GetComponentInChildren<TrainAnimationProcessor>(true) != null)
                {
                    return;
                }
                if (targetTrainObject.GetComponentsInChildren<Animator>(true).Length == 0)
                {
                    return;
                }

                targetTrainObject.AddComponent<TrainAnimationProcessor>();
            }

            #endregion
        }
    }
}

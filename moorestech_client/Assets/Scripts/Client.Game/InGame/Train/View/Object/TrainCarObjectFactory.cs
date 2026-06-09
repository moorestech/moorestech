using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Core.Master;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    ///     列車 entity を Prefab から生成し、通常描画に必要な runtime 部品を接続する。
    ///     Creates train entities from Prefabs and wires runtime-only rendering parts.
    /// </summary>
    public class TrainCarObjectFactory
    {
        private readonly TrainUnitClientCache _trainCache;
        private readonly TrainUnitTickState _tickState;
        private readonly Dictionary<Guid, GameObject> _prefabCacheByTrainCarMasterId = new();

        public TrainCarObjectFactory(TrainUnitClientCache trainCache, TrainUnitTickState tickState)
        {
            // 姿勢更新と snapshot 参照に必要な依存だけを保持する
            // Keep only dependencies required for pose updates and snapshot lookup
            _trainCache = trainCache;
            _tickState = tickState;
        }

        public TrainCarEntityObject CreateTrainCarObject(Transform parent, TrainCarSnapshot carSnapshot)
        {
            // snapshot から車両のマスタを解決する
            // Resolve train master data from the snapshot
            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(carSnapshot.TrainCarMasterId, out var trainCarMasterElement))
            {
                throw new InvalidOperationException($"TrainCar master not found. TrainCarMasterId:{carSnapshot.TrainCarMasterId}");
            }

            // Prefab は TrainCarMasterId 単位で初回だけ同期ロードする
            // Load the Prefab synchronously only once per TrainCarMasterId
            var loadedPrefab = ResolvePrefab();
            if (loadedPrefab == null)
            {
                throw new InvalidOperationException($"TrainCar prefab load failed. AddressablePath:{trainCarMasterElement.AddressablePath}");
            }

            return CreateTrainEntity(loadedPrefab);

            #region Internal

            GameObject ResolvePrefab()
            {
                if (_prefabCacheByTrainCarMasterId.TryGetValue(carSnapshot.TrainCarMasterId, out var cachedPrefab))
                {
                    return cachedPrefab;
                }

                var loaded = AddressableLoader.LoadDefault<GameObject>(trainCarMasterElement.AddressablePath);
                if (loaded != null)
                {
                    _prefabCacheByTrainCarMasterId[carSnapshot.TrainCarMasterId] = loaded;
                }
                return loaded;
            }

            TrainCarEntityObject CreateTrainEntity(GameObject prefab)
            {
                var trainObject = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);

                // entity 本体には通常描画用の ID と Rigidbody だけを初期化する
                // Initialize the entity itself with runtime id and Rigidbody setup only
                var trainEntityObject = trainObject.AddComponent<TrainCarEntityObject>();
                trainEntityObject.Initialize(carSnapshot.TrainCarInstanceId, trainCarMasterElement);

                // animation processor と座席 resolver は Prefab 構造から補完する
                // Add animation processors and seat resolver from the Prefab structure
                AttachAnimationProcessorIfNeeded(trainObject);
                AttachSeatPositionResolverIfNeeded(trainObject);

                // pose updater は Prefab 上の railposition 入力口として必須にする
                // Require the pose updater as the railposition entry point on the Prefab
                var poseUpdater = ResolvePoseUpdater(trainObject, trainCarMasterElement);
                var materialController = new TrainCarMaterialController(trainObject);
                trainEntityObject.SetMaterialController(materialController);

                // interpolator は姿勢更新だけを担当し、material は entity 側で管理する
                // Let the interpolator update pose only while the entity owns material state
                var renderInterpolator = new TrainCarEntityRenderInterpolator(trainEntityObject, poseUpdater, _trainCache, _tickState);
                trainEntityObject.SetRenderInterpolator(renderInterpolator);

                // renderer 子オブジェクトには削除 target と raycast 用 collider だけを配る
                // Give renderer children only delete targets and raycast colliders
                AttachDeleteTargets(trainObject, trainEntityObject, materialController);
                return trainEntityObject;
            }

            TrainCarRailPositionVisualPoseUpdater ResolvePoseUpdater(GameObject trainObject, TrainCarMasterElement masterElement)
            {
                var poseUpdater = trainObject.GetComponent<TrainCarRailPositionVisualPoseUpdater>();
                if (poseUpdater == null)
                {
                    throw new InvalidOperationException($"TrainCar prefab has no TrainCarRailPositionVisualPoseUpdater on root. AddressablePath:{masterElement.AddressablePath}");
                }
                return poseUpdater;
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

            void AttachSeatPositionResolverIfNeeded(GameObject targetTrainObject)
            {
                if (targetTrainObject.GetComponent<SeatPositionResolver>() != null)
                {
                    return;
                }

                targetTrainObject.AddComponent<SeatPositionResolver>();
            }

            void AttachDeleteTargets(GameObject targetTrainObject, TrainCarEntityObject trainEntityObject, TrainCarMaterialController materialController)
            {
                var meshRenderers = targetTrainObject.GetComponentsInChildren<MeshRenderer>(true);
                for (var i = 0; i < meshRenderers.Length; i++)
                {
                    var target = meshRenderers[i].gameObject;
                    var childObject = target.GetComponent<TrainCarEntityChildrenObject>();
                    if (childObject == null)
                    {
                        childObject = target.AddComponent<TrainCarEntityChildrenObject>();
                    }

                    if (target.GetComponent<MeshCollider>() == null)
                    {
                        target.AddComponent<MeshCollider>();
                    }

                    childObject.Initialize(trainEntityObject, materialController);
                }
            }

            #endregion
        }
    }
}

using System;
using Client.Common.Asset;
using Client.Game.InGame.Train.View.Object;
using Core.Master;
using Game.Train.RailPositions;
using Mooresmaster.Model.TrainModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPreviewController : MonoBehaviour
    {
        private GameObject _previewObject;
        private TrainCarRailPositionVisualPoseUpdater _poseUpdater;
        private TrainCarMaterialController _materialController;
        private ItemId _currentItemId;

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public bool ShowPreview(ItemId itemId, RailPosition railPosition, bool isPlaceable)
        {
            // 必要な時だけ preview Prefab を生成し、同じ item 中は再利用する
            // Instantiate the preview Prefab only when needed and reuse it for the same item
            if (!TryPreparePreviewObject(itemId, out _))
            {
                return false;
            }
            if (railPosition == null || railPosition.TrainLength <= 0)
            {
                return false;
            }

            // railposition から preview の姿勢を通常描画と同じ updater で決める
            // Resolve the preview pose from railposition through the same updater as runtime views
            var visualState = TrainCarRailPositionVisualState.Create(railPosition, 0, railPosition.TrainLength, true);
            if (!_poseUpdater.UpdatePose(visualState))
            {
                return false;
            }

            // 設置可否は material controller だけへ渡す
            // Send placement validity only to the material controller
            var materialMode = isPlaceable
                ? TrainCarVisualMaterialMode.PlacementPreviewPlaceable
                : TrainCarVisualMaterialMode.PlacementPreviewNotPlaceable;
            _materialController.SetMaterialMode(materialMode);
            return true;

            #region Internal

            bool TryPreparePreviewObject(ItemId targetItemId, out TrainCarMasterElement trainCarMasterElement)
            {
                trainCarMasterElement = null;
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(targetItemId, out trainCarMasterElement))
                {
                    return false;
                }

                // item が変わっていなければ object と material cache をそのまま使う
                // Reuse the object and material cache while the item is unchanged
                if (_previewObject != null && targetItemId.Equals(_currentItemId))
                {
                    return true;
                }

                DestroyPreviewObject();
                if (!TryResolvePreviewPrefab(trainCarMasterElement, out var prefab))
                {
                    return false;
                }

                // preview でも Prefab 上の pose updater と非 Mono material controller を組み合わせる
                // Combine the Prefab pose updater with the non-Mono material controller for previews
                _previewObject = Instantiate(prefab, transform);
                _previewObject.transform.localPosition = Vector3.zero;
                _previewObject.transform.localRotation = Quaternion.identity;
                _currentItemId = targetItemId;
                _poseUpdater = ResolvePoseUpdater(_previewObject, trainCarMasterElement);
                _materialController = new TrainCarMaterialController(_previewObject);
                DisableColliders(_previewObject);
                return true;
            }

            bool TryResolvePreviewPrefab(TrainCarMasterElement masterElement, out GameObject prefab)
            {
                prefab = AddressableLoader.LoadDefault<GameObject>(masterElement.AddressablePath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Train preview prefab load failed. AddressablePath:{masterElement.AddressablePath}");
                }
                return true;
            }

            TrainCarRailPositionVisualPoseUpdater ResolvePoseUpdater(GameObject previewObject, TrainCarMasterElement masterElement)
            {
                var poseUpdater = previewObject.GetComponent<TrainCarRailPositionVisualPoseUpdater>();
                if (poseUpdater == null)
                {
                    throw new InvalidOperationException($"Train preview prefab has no TrainCarRailPositionVisualPoseUpdater on root. AddressablePath:{masterElement.AddressablePath}");
                }
                return poseUpdater;
            }

            void DisableColliders(GameObject targetObject)
            {
                // preview が設置 raycast を阻害しないよう collider を無効化する
                // Disable colliders so the preview does not interfere with placement raycasts
                var colliders = targetObject.GetComponentsInChildren<Collider>(true);
                for (var i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }

            #endregion
        }

        private void OnDestroy()
        {
            DestroyPreviewObject();
        }

        private void DestroyPreviewObject()
        {
            if (_previewObject == null)
            {
                return;
            }

            // preview 破棄前に runtime material を解放する
            // Release runtime materials before discarding the preview object
            _materialController.DestroyRuntimeMaterials();
            Destroy(_previewObject);
            _previewObject = null;
            _poseUpdater = null;
            _materialController = null;
        }
    }
}

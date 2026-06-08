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
        private ITrainCarVisualTarget _visualTarget;
        private ItemId _currentItemId;

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public bool ShowPreview(ItemId itemId, RailPosition railPosition, bool isPlaceable)
        {
            // 必要な時だけpreview Prefabを生成し、同じitem中は再利用する
            // Instantiate the preview Prefab only when needed and reuse it for the same item
            if (!TryPreparePreviewObject(itemId, out _))
            {
                return false;
            }
            if (railPosition == null || railPosition.TrainLength <= 0)
            {
                return false;
            }

            // railposition描画入口は通常描画と同じvisual targetへ寄せる
            // Route railposition rendering through the same visual target used by runtime views
            var visualState = TrainCarRailPositionVisualState.Create(railPosition, 0, railPosition.TrainLength, true);
            if (!_visualTarget.UpdateVisual(visualState))
            {
                return false;
            }

            var materialMode = isPlaceable
                ? TrainCarVisualMaterialMode.PlacementPreviewPlaceable
                : TrainCarVisualMaterialMode.PlacementPreviewNotPlaceable;
            _visualTarget.SetMaterialMode(materialMode);
            return true;

            #region Internal

            bool TryPreparePreviewObject(ItemId targetItemId, out TrainCarMasterElement trainCarMasterElement)
            {
                trainCarMasterElement = null;
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(targetItemId, out trainCarMasterElement))
                {
                    return false;
                }

                // itemが変わっていなければobjectとmaterial cacheをそのまま使う
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

                _previewObject = Instantiate(prefab, transform);
                _previewObject.transform.localPosition = Vector3.zero;
                _previewObject.transform.localRotation = Quaternion.identity;
                _currentItemId = targetItemId;

                // previewにも同じ非Mono visual targetを持たせ、Prefab変更なしで入口を揃える
                // Use the same non-Mono visual target for previews without requiring Prefab changes
                _visualTarget = new TrainCarRailPositionVisualController(_previewObject);
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

            void DisableColliders(GameObject targetObject)
            {
                // previewが設置raycastを阻害しないようcolliderを無効化する
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

            // preview破棄前にruntime materialを解放する
            // Release runtime materials before discarding the preview object
            _visualTarget.DestroyRuntimeMaterials();
            Destroy(_previewObject);
            _previewObject = null;
            _visualTarget = null;
        }
    }
}

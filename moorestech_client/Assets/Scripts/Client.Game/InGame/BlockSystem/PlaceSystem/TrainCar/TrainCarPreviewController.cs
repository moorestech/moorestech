using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.View;
using Core.Master;
using Game.Train.RailPosition;
using Mooresmaster.Model.TrainModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPreviewController : MonoBehaviour
    {
        private const string DefaultTrainAddressablePath = "Vanilla/Game/DefaultTrain";
        private const float ModelYawOffsetDegrees = -90f;

        private GameObject _previewObject;
        private RendererMaterialReplacerController _materialReplacerController;
        private ItemId _currentItemId;
        private float _modelForwardCenterOffset;

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public bool ShowPreview(ItemId itemId, RailPosition railPosition, bool isPlaceable)
        {
            // プレビュー用の車両モデルを準備する
            // Prepare the preview train model
            if (!TryPreparePreviewObject(itemId, out _))
            {
                return false;
            }

            // レール位置から姿勢を算出する
            // Compute pose from rail position
            if (!TryResolvePreviewPose(railPosition, out var position, out var rotation))
            {
                return false;
            }

            // プレビューのTransformと色を更新する
            // Update preview transform and tint
            _previewObject.transform.SetPositionAndRotation(position, rotation);
            SetPlaceableColor(isPlaceable);
            return true;

            #region Internal

            bool TryPreparePreviewObject(ItemId targetItemId, out TrainCarMasterElement trainCarMasterElement)
            {
                // 車両マスターを解決する
                // Resolve the train car master
                trainCarMasterElement = null;
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(targetItemId, out trainCarMasterElement))
                {
                    return false;
                }

                // 同一アイテムなら既存プレビューを再利用する
                // Reuse the existing preview when the item is unchanged
                if (_previewObject != null && targetItemId.Equals(_currentItemId))
                {
                    return true;
                }

                // 既存プレビューを破棄して再生成する
                // Recreate the preview object
                if (_previewObject != null)
                {
                    Destroy(_previewObject);
                }
                if (!TryResolvePreviewPrefab(trainCarMasterElement, out var prefab))
                {
                    return false;
                }
                _previewObject = Instantiate(prefab, transform);
                _previewObject.transform.localPosition = Vector3.zero;
                _previewObject.transform.localRotation = Quaternion.identity;
                _materialReplacerController = new RendererMaterialReplacerController(_previewObject);
                _modelForwardCenterOffset = ResolveModelForwardCenterOffset(_previewObject.transform);
                _currentItemId = targetItemId;

                // プレビュー素材と衝突を初期化する
                // Initialize preview material and collisions
                if (!TryApplyPreviewMaterial())
                {
                    return false;
                }
                DisableColliders(_previewObject);
                return true;
            }

            bool TryResolvePreviewPrefab(TrainCarMasterElement masterElement, out GameObject prefab)
            {
                // アドレス指定のプレハブを取得する
                // Load the specified prefab
                prefab = null;
                var address = string.IsNullOrEmpty(masterElement.AddressablePath) ? DefaultTrainAddressablePath : masterElement.AddressablePath;
                prefab = AddressableLoader.LoadDefault<GameObject>(address);
                if (prefab != null)
                {
                    return true;
                }

                // フォールバックでデフォルトを使う
                // Fallback to default prefab
                prefab = AddressableLoader.LoadDefault<GameObject>(DefaultTrainAddressablePath);
                return prefab != null;
            }

            bool TryApplyPreviewMaterial()
            {
                // プレビュー材質を適用する
                // Apply preview material
                var placeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
                if (placeMaterial == null)
                {
                    return false;
                }
                _materialReplacerController.CopyAndSetMaterial(placeMaterial);
                return true;
            }

            void SetPlaceableColor(bool placeable)
            {
                // 配置可否に応じて色を切り替える
                // Switch color based on placeability
                if (_materialReplacerController == null)
                {
                    return;
                }
                var color = placeable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
                _materialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, color);
            }

            bool TryResolvePreviewPose(RailPosition targetRailPosition, out Vector3 position, out Quaternion rotation)
            {
                // 出力を初期化する
                // Initialize outputs
                position = default;
                rotation = Quaternion.identity;

                // レール位置と長さを検証する
                // Validate rail position and length
                if (targetRailPosition == null || targetRailPosition.TrainLength <= 0)
                {
                    return false;
                }

                // 前後輪から中心姿勢を算出する
                // Compute center pose from front and rear positions
                if (!TrainCarPoseCalculator.TryGetPose(targetRailPosition, 0, out var frontPosition, out var frontForward))
                {
                    return false;
                }
                if (!TrainCarPoseCalculator.TryGetPose(targetRailPosition, targetRailPosition.TrainLength, out var rearPosition, out _))
                {
                    return false;
                }
                var centerPosition = (frontPosition + rearPosition) * 0.5f;
                var delta = frontPosition - rearPosition;
                var forward = delta.sqrMagnitude > 1e-6f ? delta.normalized : (frontForward.sqrMagnitude > 1e-6f ? frontForward.normalized : Vector3.forward);
                rotation = BuildRotation(forward);

                // モデルの前後補正を適用する
                // Apply model forward offset correction
                var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
                var modelForward = rotation * localForwardAxis;
                position = centerPosition - modelForward * _modelForwardCenterOffset;
                return true;
            }

            Quaternion BuildRotation(Vector3 forward)
            {
                // 向きから回転を構成する
                // Build rotation from forward vector
                var safeForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
                var rotation = Quaternion.LookRotation(safeForward, Vector3.up);
                return rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            }

            float ResolveModelForwardCenterOffset(Transform targetTransform)
            {
                // レンダラの重心から前後オフセットを算出する
                // Compute forward offset from renderer bounds
                var renderers = targetTransform.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                {
                    return 0f;
                }
                var combined = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }
                var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
                var localCenter = targetTransform.InverseTransformPoint(combined.center);
                return Vector3.Dot(localCenter, localForwardAxis);
            }

            void DisableColliders(GameObject targetObject)
            {
                // レイキャスト干渉を防ぐためコライダーを無効化する
                // Disable colliders to avoid raycast interference
                var colliders = targetObject.GetComponentsInChildren<Collider>(true);
                for (var i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }

            #endregion
        }
    }
}

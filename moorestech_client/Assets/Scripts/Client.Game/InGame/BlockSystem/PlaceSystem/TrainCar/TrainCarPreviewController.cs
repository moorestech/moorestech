using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.View;
using Core.Master;
using Game.Train.RailPositions;
using Mooresmaster.Model.TrainModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public class TrainCarPreviewController : MonoBehaviour
    {
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
            // プレビュー用の列車モデルを準備する
            // Prepare the preview train model
            if (!TryPreparePreviewObject(itemId, out _))
            {
                return false;
            }

            // レール位置から姿勢を計算する
            // Compute pose from rail position
            if (!TryResolvePreviewPose(railPosition, out var position, out var rotation))
            {
                return false;
            }

            // プレビューの Transform と色を更新する
            // Update preview transform and tint
            _previewObject.transform.SetPositionAndRotation(position, rotation);
            SetPlaceableColor(isPlaceable);
            return true;

            #region Internal

            bool TryPreparePreviewObject(ItemId targetItemId, out TrainCarMasterElement trainCarMasterElement)
            {
                // 列車マスターを解決する
                // Resolve the train car master
                trainCarMasterElement = null;
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(targetItemId, out trainCarMasterElement))
                {
                    return false;
                }

                // 同じアイテムなら既存プレビューを再利用する
                // Reuse the existing preview when the item is unchanged
                if (_previewObject != null && targetItemId.Equals(_currentItemId))
                {
                    return true;
                }

                // 既存プレビューを破棄して作り直す
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

                // プレビュー材質と衝突設定を初期化する
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
                // 指定された prefab をそのまま読む
                // Load the specified prefab directly
                prefab = AddressableLoader.LoadDefault<GameObject>(masterElement.AddressablePath);
                if (prefab == null)
                {
                    throw new System.InvalidOperationException($"Train preview prefab load failed. AddressablePath:{masterElement.AddressablePath}");
                }
                return true;
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
                // 設置可否に応じて色を切り替える
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

                // 前後位置から中央姿勢を計算する
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

                // モデル中心補正を適用する
                // Apply model forward offset correction
                var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
                var modelForward = rotation * localForwardAxis;
                position = centerPosition - modelForward * _modelForwardCenterOffset;
                return true;
            }

            Quaternion BuildRotation(Vector3 forward)
            {
                // 前方ベクトルから回転を構築する
                // Build rotation from forward vector
                var safeForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
                var rotation = Quaternion.LookRotation(safeForward, Vector3.up);
                return rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            }

            float ResolveModelForwardCenterOffset(Transform targetTransform)
            {
                // renderer bounds から前方中心オフセットを計算する
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
                // レイキャスト干渉を避けるため collider を無効化する
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

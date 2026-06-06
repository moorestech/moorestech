using System;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.View;
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
        private RendererMaterialReplacerController _materialReplacerController;
        private TrainCarPoseService _poseService;
        private ItemId _currentItemId;
        private float _modelForwardCenterOffset;
        private int _visualPartCount;
        private int[] _visualPartAuthoredLengths = Array.Empty<int>();
        private int[] _visualPartNormalizedLengths = Array.Empty<int>();
        private TrainCarPartSpan[] _visualPartSpans = Array.Empty<TrainCarPartSpan>();

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
            if (!TryApplyPreviewVisualPartPoses(railPosition))
            {
                return false;
            }
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
                    _poseService = null;
                }
                if (!TryResolvePreviewPrefab(trainCarMasterElement, out var prefab))
                {
                    return false;
                }
                _previewObject = Instantiate(prefab, transform);
                _previewObject.transform.localPosition = Vector3.zero;
                _previewObject.transform.localRotation = Quaternion.identity;
                _materialReplacerController = new RendererMaterialReplacerController(_previewObject);
                _poseService = new TrainCarPoseService(_previewObject.transform, _previewObject.GetComponentsInChildren<Renderer>(true));
                _modelForwardCenterOffset = _poseService.ModelForwardCenterOffset;
                InitializePreviewVisualPartBuffers();
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
                var placeMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
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
                if (!TrainCarPoseCalculator.TryResolveRenderPose(targetRailPosition, 0, targetRailPosition.TrainLength, true, _modelForwardCenterOffset, out position, out rotation))
                {
                    return false;
                }
                return true;
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

        private void InitializePreviewVisualPartBuffers()
        {
            // Prefab marker から preview part buffer を初期化する
            // Initialize preview part buffers from Prefab markers
            _visualPartCount = _poseService.GetVisualPartCount();
            if (_visualPartCount <= 0)
            {
                _visualPartAuthoredLengths = Array.Empty<int>();
                _visualPartNormalizedLengths = Array.Empty<int>();
                _visualPartSpans = Array.Empty<TrainCarPartSpan>();
                return;
            }

            // item が同じ間は buffer と preview object を使い回す
            // Reuse buffers and the preview object while the item is unchanged
            _visualPartAuthoredLengths = new int[_visualPartCount];
            _visualPartNormalizedLengths = new int[_visualPartCount];
            _visualPartSpans = new TrainCarPartSpan[_visualPartCount];
            for (var i = 0; i < _visualPartCount; i++)
            {
                _poseService.TryGetVisualPartLengthMeters(i, out _visualPartAuthoredLengths[i]);
            }
        }

        private bool TryApplyPreviewVisualPartPoses(RailPosition railPosition)
        {
            // 分割表示がない preview は root pose のみで完了する
            // Preview without split visuals completes with the root pose only
            if (_visualPartCount <= 0)
            {
                return true;
            }
            if (_poseService == null || railPosition == null || railPosition.TrainLength <= 0)
            {
                return false;
            }

            // preview 用の車両長へ part 比率を正規化する
            // Normalize part ratios to the preview car length
            if (!TrainCarPartPoseCalculator.TryBuildNormalizedPartLengths(railPosition.TrainLength, _visualPartAuthoredLengths, _visualPartNormalizedLengths, out var partCount))
            {
                return false;
            }
            var partStartOffset = 0;
            for (var i = 0; i < partCount; i++)
            {
                if (!TryApplyPreviewVisualPartPose(i, partStartOffset, _visualPartNormalizedLengths[i]))
                {
                    return false;
                }
                partStartOffset += _visualPartNormalizedLengths[i];
            }
            return true;

            #region Internal

            bool TryApplyPreviewVisualPartPose(int index, int partStartOffset, int partLength)
            {
                // preview は既存挙動に合わせて facing forward として姿勢を解く
                // Resolve preview pose as facing-forward to preserve existing preview behavior
                if (!TrainCarPartPoseCalculator.TryBuildPartSpan(0, railPosition.TrainLength, partStartOffset, partLength, true, out _visualPartSpans[index]))
                {
                    return false;
                }
                if (!_poseService.TryGetVisualPartModelForwardCenterOffset(index, out var modelForwardCenterOffset))
                {
                    return false;
                }

                // part pose を解いて preview target に反映する
                // Resolve and apply the part pose to the preview target
                var span = _visualPartSpans[index];
                if (!TrainCarPoseCalculator.TryResolveRenderPose(railPosition, span.FrontOffset, span.RearOffset, true, modelForwardCenterOffset, out var position, out var rotation))
                {
                    return false;
                }
                _poseService.RequestPartPose(index, position, rotation);
                return true;
            }

            #endregion
        }
    }
}

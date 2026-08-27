using System;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Player;
using Client.Game.InGame.Tutorial.TutorialBlock;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.ChallengesModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     最寄りのアンカーブロック原点＋offset にゴーストを出し、そこへ対象ブロックが置かれたら完了する
    ///     Shows a ghost at nearest-anchor origin + offset and completes when the target block lands there
    /// </summary>
    public class RelativeBlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        private const string WebPinId = "relative-block-place-preview-pin";

        public bool IsApplied => _currentParam != null;

        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private TutorialBlockPreviewObject _previewObject;
        private RelativeBlockPlacePreviewTutorialParam _currentParam;
        private BlockId _anchorBlockId;
        private BlockId _targetBlockId;
        private BlockDirection _direction;
        private Vector3Int? _targetCell;
        private IDisposable _blockPlacedDisposable;
        private string _pinTutorialGuid = "";

        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentParam = (RelativeBlockPlacePreviewTutorialParam)tutorial.TutorialParam;
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");
            _anchorBlockId = MasterHolder.BlockMaster.GetBlockId(_currentParam.AnchorBlockGuid);
            _targetBlockId = MasterHolder.BlockMaster.GetBlockId(_currentParam.BlockGuid);
            _direction = Enum.Parse<BlockDirection>(_currentParam.BlockDirection);
            _targetCell = null;

            if (_blockGameObjectDataStore != null) SubscribePlacementEvent();
            return this;
        }

        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;
            HidePreview();
            _currentParam = null;
        }

        private void Update()
        {
            if (_currentParam == null || _blockGameObjectDataStore == null) return;

            // アンカーは撤去や増設で変わるため毎フレーム最寄りを取り直す（VeinPin と同じ追従）
            // The anchor can be removed or duplicated, so re-pick the nearest one every frame (same tracking as VeinPin)
            var anchor = FindNearestAnchor();
            if (anchor == null)
            {
                HidePreview();
                return;
            }

            var cell = anchor.BlockPosInfo.OriginalPos + _currentParam.Offset;
            if (_targetCell != cell)
            {
                _targetCell = cell;
                ShowPreviewAsync(cell).Forget();
            }
            PublishWebPin();

            #region Internal

            BlockGameObject FindNearestAnchor()
            {
                var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                BlockGameObject nearest = null;
                var nearestSqr = float.MaxValue;
                foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
                {
                    if (block.BlockId != _anchorBlockId) continue;
                    var sqr = (block.transform.position - playerPosition).sqrMagnitude;
                    if (sqr >= nearestSqr) continue;
                    nearestSqr = sqr;
                    nearest = block;
                }
                return nearest;
            }

            void PublishWebPin()
            {
                if (!WebUiScreenGate.IsWebUiMode || _previewObject == null) return;
                var camera = CameraManager.MainCamera.Camera;
                if (!camera) return;
                var projection = WorldPinScreenProjection.Project(camera, _previewObject.transform.position);
                WorldPinStateStore.Instance.SetPin(WebPinId, _pinTutorialGuid, projection);
            }

            #endregion
        }

        private async UniTaskVoid ShowPreviewAsync(Vector3Int cell)
        {
            if (_previewObject == null || _previewObject.BlockMasterElement.BlockGuid != _currentParam.BlockGuid)
            {
                if (_previewObject != null) _previewObject.DestroyPreview();
                _previewObject = await TutorialPreviewBlockCreator.CreateAsync(_targetBlockId);
                _previewObject.transform.SetParent(transform);
            }

            var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(cell, _direction, _targetBlockId);
            _previewObject.SetTransform(position, _direction.GetRotation());
            _previewObject.SetPlaceableColor(true);
            _previewObject.SetActive(true);
        }

        private void SubscribePlacementEvent()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
            {
                if (block.BlockId != _targetBlockId) return;
                if (_targetCell == null || block.BlockPosInfo.OriginalPos != _targetCell.Value) return;
                CompleteTutorial();
            });
        }

        private void HidePreview()
        {
            _targetCell = null;
            if (_previewObject != null) _previewObject.SetActive(false);
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }

        private void OnDestroy()
        {
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }
    }
}

using System;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
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

namespace Client.Game.InGame.Tutorial
{
    public class BlockPlacePreviewTutorialManager : MonoBehaviour, ITutorialView, ITutorialViewManager
    {
        // WebオーバーレイでのピンID。BlockPlacePreviewTutorialManagerはシーンに1つなので固定IDでよい
        // World-pin id on the web overlay; a single scene instance suffices, so the id is fixed
        private const string WebPinId = "block-place-preview-pin";

        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private TutorialBlockPreviewObject _previewObject;
        private BlockPlacePreviewTutorialParam _currentParam;
        private BlockId _currentBlockId;
        private IDisposable _blockPlacedDisposable;

        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        private void Update()
        {
            // Webへ射影配信する（3Dプレビュー自体はUnity側に残置し、矢印/ピンのみWeb化）
            // Project and publish to the web overlay (the 3D preview stays in Unity; only the arrow/pin moves to web)
            if (!WebUiScreenGate.IsWebUiMode || _currentParam == null || _previewObject == null) return;

            var camera = CameraManager.MainCamera.Camera;
            if (!camera) return;

            var projection = WorldPinScreenProjection.Project(camera, _previewObject.transform.position);
            WorldPinStateStore.Instance.SetPin(WebPinId, _currentParam.Message, projection);
        }

        public ITutorialView ApplyTutorial(ITutorialParam param)
        {
            _currentParam = (BlockPlacePreviewTutorialParam)param;
            _currentBlockId = MasterHolder.BlockMaster.GetBlockId(_currentParam.BlockGuid);

            // 既に目標ブロックが配置済みなら早期終了
            // Exit early when the target block already exists
            if (IsTargetBlockPlaced())
            {
                _currentParam = null;
                return null;
            }

            CreateOrUpdatePreviewAsync().Forget();
            SubscribePlacementEvent();

            return this;

            #region Internal

            bool IsTargetBlockPlaced()
            {
                return _blockGameObjectDataStore.TryGetBlockGameObject(_currentParam.Position, out var block)
                       && block.BlockId == _currentBlockId;
            }

            async UniTaskVoid CreateOrUpdatePreviewAsync()
            {
                // プレビューオブジェクトを生成または再利用
                // Create or reuse preview object
                if (_previewObject == null || _previewObject.BlockMasterElement.BlockGuid != _currentParam.BlockGuid)
                {
                    if (_previewObject != null)
                    {
                        _previewObject.DestroyPreview();
                    }

                    _previewObject = await TutorialPreviewBlockCreator.CreateAsync(_currentBlockId);
                    _previewObject.transform.SetParent(transform);
                }

                var blockDirection = Enum.Parse<BlockDirection>(_currentParam.BlockDirection);
                var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(_currentParam.Position, blockDirection, _currentBlockId);
                var rotation = blockDirection.GetRotation();

                _previewObject.SetTransform(position, rotation);
                _previewObject.SetPlaceableColor(true);
                _previewObject.SetActive(true);
            }

            void SubscribePlacementEvent()
            {
                _blockPlacedDisposable?.Dispose();
                
                // 指定座標へのブロック設置を監視
                // Watch for block placement at the specified position
                _blockPlacedDisposable = _blockGameObjectDataStore.OnBlockPlaced.Subscribe(block =>
                {
                    if (block.BlockId != _currentBlockId) return;
                    if (block.BlockPosInfo.OriginalPos != _currentParam.Position) return;
                    
                    CompleteTutorial();
                });
            }
            
            #endregion
        }

        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;

            // プレビューを非表示にしてHUDを更新
            // Hide the preview and update HUD state
            if (_previewObject != null)
            {
                _previewObject.SetActive(false);
            }

            // Webピンは冪等に除去（未配信でも安全）
            // Removing the web pin is idempotent, safe even when never published
            WorldPinStateStore.Instance.RemovePin(WebPinId);

            _currentParam = null;
        }

        private void OnDestroy()
        {
            WorldPinStateStore.Instance.RemovePin(WebPinId);
        }
    }
}

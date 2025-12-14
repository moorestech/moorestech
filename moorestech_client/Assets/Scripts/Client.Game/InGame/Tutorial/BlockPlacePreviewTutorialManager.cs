using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Core.Master;
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
        [SerializeField] private Transform previewRoot;

        private BlockGameObjectDataStore _blockGameObjectDataStore;
        private BlockPreviewObject _previewObject;
        private BlockPlacePreviewTutorialParam _currentParam;
        private BlockId _currentBlockId;
        private IDisposable _blockPlacedDisposable;

        [Inject]
        public void Construct(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
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

            CreateOrUpdatePreview();
            SubscribePlacementEvent();

            return this;
        }

        public void CompleteTutorial()
        {
            _blockPlacedDisposable?.Dispose();
            _blockPlacedDisposable = null;

            // プレビューを非表示にしてHUDを更新
            // Hide the preview and update HUD state
            if (_previewObject != null)
            {
                HudArrowManager.UnregisterHudArrowTarget(_previewObject.gameObject);
                _previewObject.SetActive(false);
            }

            _currentParam = null;
        }

        private void CreateOrUpdatePreview()
        {
            var previewParent = previewRoot ? previewRoot : transform;

            // プレビューオブジェクトを生成または再利用
            // Create or reuse preview object
            if (_previewObject == null || _previewObject.BlockMasterElement.BlockGuid != _currentParam.BlockGuid)
            {
                if (_previewObject != null)
                {
                    HudArrowManager.UnregisterHudArrowTarget(_previewObject.gameObject);
                    _previewObject.Destroy();
                }

                _previewObject = PreviewBlockCreator.Create(_currentBlockId);
                _previewObject.transform.SetParent(previewParent);
            }

            var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(_currentParam.Position, _currentParam.BlockDirection, _currentBlockId);
            var rotation = _currentParam.BlockDirection.GetRotation();

            _previewObject.SetTransform(position, rotation);
            _previewObject.SetPlaceableColor(true);
            _previewObject.SetActive(true);
            HudArrowManager.RegisterHudArrowTarget(_previewObject.gameObject, new HudArrowOptions(hideWhenTargetInactive: false));
        }

        private void SubscribePlacementEvent()
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

        private bool IsTargetBlockPlaced()
        {
            return _blockGameObjectDataStore.TryGetBlockGameObject(_currentParam.Position, out var block)
                   && block.BlockId == _currentBlockId;
        }
    }
}

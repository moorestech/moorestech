using System.Threading;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Tutorial.TutorialBlock;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     ゴースト1体の実体。WebピンIDはtutorialGuid由来
    ///     One tutorial ghost instance; its web pin id derives from the tutorialGuid so entries stay independent
    /// </summary>
    public class TutorialGhostEntry
    {
        public string TutorialGuid { get; }
        public string WebPinId { get; }
        public TutorialBlockPreviewObject PreviewObject { get; private set; }
        public BlockId TargetBlockId { get; private set; }
        public Vector3Int? TargetCell { get; private set; }
        public BlockDirection TargetDirection { get; private set; }
        
        private BlockId _previewObjectBlockId;
        
        // ゴースト生成はAddressableロードを挟むため、await明けに古い対象へ書き戻さないようトークンで打ち切る
        // Ghost creation awaits an Addressable load, so a token cancels it instead of writing back to a stale target
        private CancellationTokenSource _previewCancellation;
        
        public TutorialGhostEntry(string tutorialGuid)
        {
            TutorialGuid = tutorialGuid;
            WebPinId = $"block-place-preview-pin-{tutorialGuid}";
        }
        
        public bool IsSameTarget(BlockId blockId, Vector3Int cell, BlockDirection direction)
        {
            return TargetCell == cell && TargetBlockId == blockId && TargetDirection == direction;
        }
        
        public void SetTarget(BlockId blockId, Vector3Int cell, BlockDirection direction, Transform parent)
        {
            TargetBlockId = blockId;
            TargetCell = cell;
            TargetDirection = direction;
            
            CancelPendingPreview();
            _previewCancellation = new CancellationTokenSource();
            ShowPreviewAsync(parent, _previewCancellation.Token).Forget();
        }
        
        public void Hide()
        {
            CancelPendingPreview();
            TargetCell = null;
            if (PreviewObject != null) PreviewObject.SetActive(false);
        }
        
        public void Destroy()
        {
            Hide();
            if (PreviewObject != null) PreviewObject.DestroyPreview();
            PreviewObject = null;
        }
        
        private async UniTaskVoid ShowPreviewAsync(Transform parent, CancellationToken cancellationToken)
        {
            // 対象変更時のみゴースト再生成
            // Recreate the ghost only when the target block kind changed
            if (PreviewObject == null || _previewObjectBlockId != TargetBlockId)
            {
                if (PreviewObject != null) PreviewObject.DestroyPreview();
                
                var created = await TutorialPreviewBlockCreator.CreateAsync(TargetBlockId, cancellationToken);
                if (created == null) return;
                
                PreviewObject = created;
                _previewObjectBlockId = TargetBlockId;
                PreviewObject.transform.SetParent(parent);
            }
            
            if (TargetCell == null) return;
            
            var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(TargetCell.Value, TargetDirection, TargetBlockId);
            PreviewObject.SetTransform(position, TargetDirection.GetRotation());
            PreviewObject.SetPlaceableColor(true);
            PreviewObject.SetActive(true);
        }
        
        private void CancelPendingPreview()
        {
            if (_previewCancellation == null) return;
            
            _previewCancellation.Cancel();
            _previewCancellation.Dispose();
            _previewCancellation = null;
        }
    }
}

using System.Threading;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost
{
    /// <summary>
    ///     設置ゴースト1体分の実体。WebピンIDは呼び手が渡す
    ///     One placement ghost instance; the caller supplies the web pin id, so this type knows nothing about tutorial identity
    /// </summary>
    public class PlacementGhostEntry
    {
        public string WebPinId { get; }
        public PreviewGhostObject PreviewObject { get; private set; }
        public Vector3Int? TargetCell { get; private set; }

        private BlockId _targetBlockId;
        private BlockDirection _targetDirection;

        // 生成要求済みのブロック種別。種別が変わった時だけ作り直す（セル移動でキャンセルすると追従中に一度も出ない）
        // The requested block kind; recreate only when it changes (cancelling on cell moves would never show a ghost while tracking)
        private BlockId? _requestedBlockId;
        
        // ゴースト生成はAddressableロードを挟むため、種別変更時はトークンで打ち切り古い対象への書き戻しを防ぐ
        // Ghost creation awaits an Addressable load; a kind change cancels via token so a stale target is never written back
        private CancellationTokenSource _previewCancellation;
        
        public PlacementGhostEntry(string webPinId)
        {
            WebPinId = webPinId;
        }
        
        /// <summary>
        ///     ゴーストの目標セルを差し替える。毎フレーム呼んでよい
        ///     Replaces the target cell the ghost points at; an unchanged target is a no-op, so it is safe to call every frame
        /// </summary>
        public void SetTarget(BlockId blockId, Vector3Int cell, BlockDirection direction, Transform parent)
        {
            if (TargetCell == cell && _targetBlockId == blockId && _targetDirection == direction) return;

            _targetBlockId = blockId;
            TargetCell = cell;
            _targetDirection = direction;
            
            // 同種別は同期移動、生成中は完了時に着地
            // Same kind: move an existing ghost synchronously; an in-flight creation lands on the latest values when it completes
            if (_requestedBlockId == blockId)
            {
                if (PreviewObject != null) ApplyTargetTransform();
                return;
            }
            
            _requestedBlockId = blockId;
            CancelPendingPreview();
            _previewCancellation = new CancellationTokenSource();
            ShowPreviewAsync(_previewCancellation.Token).Forget();
            
            #region Internal
            
            async UniTaskVoid ShowPreviewAsync(CancellationToken cancellationToken)
            {
                if (PreviewObject != null) PreviewObject.DestroyPreview();
                
                var created = await PreviewGhostCreator.CreateAsync(blockId, cancellationToken);
                if (created == null) return;
                
                PreviewObject = created;
                PreviewObject.transform.SetParent(parent);
                
                // await後の最新の目標へ配置する
                // Creation done; place at the latest target that may have moved during the await
                _previewCancellation?.Dispose();
                _previewCancellation = null;
                if (TargetCell == null) return;
                ApplyTargetTransform();
            }
            
            #endregion
        }
        
        public void Hide()
        {
            // 生成中に隠すと生成物は捨てられるので要求も取り消す。残すと同種別の次回SetTargetが「要求済み」で素通りし二度と出ない
            // Hiding mid-creation discards the result, so drop the request too; keeping it makes the next same-kind SetTarget skip as "requested" and the ghost never returns
            if (_previewCancellation != null) _requestedBlockId = null;
            CancelPendingPreview();
            TargetCell = null;
            if (PreviewObject != null) PreviewObject.SetActive(false);
        }
        
        public void Destroy()
        {
            Hide();
            if (PreviewObject != null) PreviewObject.DestroyPreview();
            PreviewObject = null;
            _requestedBlockId = null;
        }
        
        private void ApplyTargetTransform()
        {
            var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(TargetCell.Value, _targetDirection, _targetBlockId);
            PreviewObject.SetTransform(position, _targetDirection.GetRotation());
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

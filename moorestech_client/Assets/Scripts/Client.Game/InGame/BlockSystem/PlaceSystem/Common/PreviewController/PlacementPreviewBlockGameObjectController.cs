using System.Collections.Generic;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    public class PlacementPreviewBlockGameObjectController : MonoBehaviour, IPlacementPreviewBlockGameObjectController
    {
        private BlockMasterElement _previewBlockMasterElement;
        private BlockPlacePreviewObjectPool _blockPlacePreviewObjectPool;
        private readonly List<BlockPreviewObject> _activePreviewBlocks = new();
        
        public bool IsActive => gameObject.activeSelf;
        
        
        private void Awake()
        {
            _blockPlacePreviewObjectPool = new BlockPlacePreviewObjectPool(transform);
            SetActive(false);
        }
        
        public void SetPreview(List<PlaceInfo> placePointInfos, BlockMasterElement holdingBlockMaster)
        {
            // さっきと違うブロックだったら削除する
            // Destroy the pooled previews when the held block changed
            if (_previewBlockMasterElement == null || _previewBlockMasterElement.BlockGuid != holdingBlockMaster.BlockGuid)
            {
                _previewBlockMasterElement = holdingBlockMaster;
                _blockPlacePreviewObjectPool.AllDestroy();
            }
            
            _blockPlacePreviewObjectPool.AllUnUse();
            _activePreviewBlocks.Clear();

            // プレビューブロックの位置を設定
            // Set preview block positions
            foreach (var placeInfo in placePointInfos)
            {
                var blockId = placeInfo.BlockId;

                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placeInfo.Position, placeInfo.Direction, blockId);
                var rot = placeInfo.Direction.GetRotation();

                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                _activePreviewBlocks.Add(previewBlock);
                previewBlock.SetTransform(pos,rot);

                previewBlock.SetPreviewStateDetail(placeInfo);
            }

            // 可否色は一箇所で塗る
            // Placeable colors are painted in one place
            UpdatePlaceableColors(placePointInfos);
        }
        
        public IReadOnlyList<bool> DetectGroundOverlaps()
        {
            // 直近の物理ステップ時点の接触を返す。可否も色も決めない
            // Returns the contact as of the last physics step; placeability and color stay with the caller
            var groundOverlaps = new List<bool>(_activePreviewBlocks.Count);
            foreach (var previewBlock in _activePreviewBlocks) groundOverlaps.Add(previewBlock.IsCollisionGround);

            return groundOverlaps;
        }
        
        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos)
        {
            for (var i = 0; i < _activePreviewBlocks.Count && i < placeInfos.Count; i++)
            {
                _activePreviewBlocks[i].SetPlaceableColor(placeInfos[i].Placeable);
            }
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
        {
            // 直前のSetPreviewの並び順と一致
            // Matches the ordering of the preceding SetPreview call
            previewBlock = 0 <= index && index < _activePreviewBlocks.Count ? _activePreviewBlocks[index] : null;
            return previewBlock != null;
        }
    }
}
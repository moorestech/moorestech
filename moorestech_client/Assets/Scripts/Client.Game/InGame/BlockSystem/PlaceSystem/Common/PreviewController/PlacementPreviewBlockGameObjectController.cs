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

                previewBlock.SetPlaceableColor(placeInfo.Placeable);
                previewBlock.SetPreviewStateDetail(placeInfo);
            }
        }
        
        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> placePointInfos, BlockMasterElement holdingBlockMaster)
        {
            SetPreview(placePointInfos, holdingBlockMaster);

            // 地形接触を見る系統だけが初期色にも接触を織り込む
            // Only the terrain-aware systems fold contact into the initial color as well
            var isGroundDetectedList = new List<bool>();
            for (var i = 0; i < _activePreviewBlocks.Count; i++)
            {
                var isGroundDetected = _activePreviewBlocks[i].IsCollisionGround;
                isGroundDetectedList.Add(isGroundDetected);

                _activePreviewBlocks[i].SetPlaceableColor(!isGroundDetected && placePointInfos[i].Placeable);
            }

            return isGroundDetectedList;
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
            // アクティブなプレビューブロックをインデックスで取り出す（SetPreviewAndGroundDetectの順序と一致）
            // Fetch an active preview block by index, matching SetPreviewAndGroundDetect ordering
            previewBlock = 0 <= index && index < _activePreviewBlocks.Count ? _activePreviewBlocks[index] : null;
            return previewBlock != null;
        }
    }
}
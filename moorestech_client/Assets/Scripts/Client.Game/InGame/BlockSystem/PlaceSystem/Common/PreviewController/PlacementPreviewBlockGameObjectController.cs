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
        
        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> placePointInfos, BlockMasterElement holdingBlockMaster)
        {
            // さっきと違うブロックだったら削除する
            if (_previewBlockMasterElement == null || _previewBlockMasterElement.BlockGuid != holdingBlockMaster.BlockGuid)
            {
                _previewBlockMasterElement = holdingBlockMaster;
                _blockPlacePreviewObjectPool.AllDestroy();
            }
            
            _blockPlacePreviewObjectPool.AllUnUse();
            _activePreviewBlocks.Clear();

            // プレビューブロックの位置を設定
            // Set preview block positions
            var isGroundDetectedList = new List<bool>();
            foreach (var placeInfo in placePointInfos)
            {
                var blockId = placeInfo.BlockId;

                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placeInfo.Position, placeInfo.Direction, blockId);
                var rot = placeInfo.Direction.GetRotation();

                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                _activePreviewBlocks.Add(previewBlock);
                previewBlock.SetTransform(pos,rot);
                var isGroundDetected = previewBlock.IsCollisionGround;
                isGroundDetectedList.Add(isGroundDetected);

                // 地面接触時は設置不可扱いとして色分岐に渡す
                // Treat ground-collided cells as not placeable when deciding preview color
                var groundAwarePlaceInfo = new PlaceInfo { Placeable = !isGroundDetected && placeInfo.Placeable, IsReplace = placeInfo.IsReplace };
                previewBlock.SetPreviewColor(groundAwarePlaceInfo);
                previewBlock.SetPreviewStateDetail(placeInfo);
            }

            return isGroundDetectedList;
        }
        
        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos)
        {
            for (var i = 0; i < _activePreviewBlocks.Count && i < placeInfos.Count; i++)
            {
                _activePreviewBlocks[i].SetPreviewColor(placeInfos[i]);
            }
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
    }
}
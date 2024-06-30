using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        private BlockConfigData _previewBlockConfig;
        private BlockPlacePreviewObjectPool _blockPlacePreviewObjectPool;
        private GroundCollisionDetector[] _collisionDetectors;
        
        public bool IsActive => gameObject.activeSelf;
        
        public bool IsCollisionGround
        {
            get
            {
                if (_collisionDetectors == null) return false;
                return _collisionDetectors.Any(detector => detector.IsCollision);
            }
        }
        
        private readonly Dictionary<(int, BlockVerticalDirection), int> _blockVerticalDictionary = new();
        
        private void Start()
        {
            //TODO ここをコンフィグに入れる
            var gearBeltConveyorId = ServerContext.BlockConfig.GetBlockConfig(AlphaMod.ModId, "gear belt conveyor").BlockId;
            var gearBeltConveyorUpId = ServerContext.BlockConfig.GetBlockConfig(AlphaMod.ModId, "gear belt conveyor up").BlockId;
            _blockVerticalDictionary.Add((gearBeltConveyorId, BlockVerticalDirection.Up), gearBeltConveyorUpId);
            var gearBeltConveyorDownId = ServerContext.BlockConfig.GetBlockConfig(AlphaMod.ModId, "gear belt conveyor down").BlockId;
            _blockVerticalDictionary.Add((gearBeltConveyorId, BlockVerticalDirection.Down), gearBeltConveyorDownId);
            
            _blockPlacePreviewObjectPool = new BlockPlacePreviewObjectPool(transform);
        }
        
        public void SetPreview(bool placeable, Vector3Int startPoint, Vector3Int endPoint, bool isStartZDirection, BlockDirection specifiedDirection, BlockConfigData blockConfig)
        {
            CreatePreviewObjects(startPoint, endPoint, isStartZDirection, specifiedDirection, blockConfig);
            
            var materialPath = placeable ? MaterialConst.PreviewPlaceBlockMaterial : MaterialConst.PreviewNotPlaceableBlockMaterial;
            //SetMaterial(Resources.Load<Material>(materialPath));
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        private void CreatePreviewObjects(Vector3Int startPoint, Vector3Int endPoint, bool isStartZDirection, BlockDirection specifiedDirection, BlockConfigData blockConfig)
        {
            var placePointInfos = BlockPlacePointCalculator.CalculatePoint(startPoint, endPoint, isStartZDirection, specifiedDirection);
            
            // さっきと違うブロックだったら削除する
            if (_previewBlockConfig == null || _previewBlockConfig.BlockId != blockConfig.BlockId)
            {
                _previewBlockConfig = blockConfig;
                _blockPlacePreviewObjectPool.AllDestroy();
            }
            
            _blockPlacePreviewObjectPool.AllUnUse();
            
            // プレビューブロックの位置を設定
            for (var i = 0; i < placePointInfos.Count; i++)
            {
                var placePoint = placePointInfos[i].Point;
                var direction = placePointInfos[i].Direction;
                var verticalDirection = placePointInfos[i].VerticalDirection;
                
                var blockId = blockConfig.BlockId;
                if (_blockVerticalDictionary.TryGetValue((blockId, verticalDirection), out var verticalBlockId))
                {
                    blockId = verticalBlockId;
                }
                
                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placePoint, direction, blockId);
                var rot = direction.GetRotation();
                
                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                previewBlock.transform.position = pos;
                previewBlock.transform.rotation = rot;
            }
        }
        
        private BlockPreviewObject CreatePreviewObject(BlockConfigData blockConfig)
        {
            var previewBlock = ClientContext.BlockGameObjectContainer.CreatePreviewBlock(blockConfig.BlockId);
            previewBlock.transform.SetParent(transform);
            previewBlock.transform.localPosition = Vector3.zero;
            _collisionDetectors = previewBlock.GetComponentsInChildren<GroundCollisionDetector>();
            
            return previewBlock;
        }
        
        public void SetMaterial(Material material)
        {
            //プレビューブロックのマテリアルを変更
            // foreach (var previewBlock in _previewBlocks)
            // {
            //     previewBlock.SetMaterial(material);
            // }
        }
    }
}
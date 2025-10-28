using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.PreviewController
{
    public class BlockPlacePreviewObjectPool
    {
        private readonly Transform _parentTransform;
        private readonly Dictionary<BlockId, List<PreviewObject>> _blockPreviewObjects = new();
        
        public BlockPlacePreviewObjectPool(Transform parentTransform)
        {
            _parentTransform = parentTransform;
        }
        
        class PreviewObject
        {
            public BlockPreviewObject BlockPreviewObject;
            public bool IsUsed;
        }
        
        public BlockPreviewObject GetObject(BlockId blockId)
        {
            if (!_blockPreviewObjects.ContainsKey(blockId))
            {
                _blockPreviewObjects.Add(blockId, new List<PreviewObject>());
            }
            
            var unusedObject = _blockPreviewObjects[blockId].Find(obj => !obj.IsUsed);
            if (unusedObject == null)
            {
                unusedObject = new PreviewObject
                {
                    BlockPreviewObject = CreatePreviewObject(blockId),
                    IsUsed = true
                };
                _blockPreviewObjects[blockId].Add(unusedObject);
            }
            else
            {
                unusedObject.IsUsed = true;
                unusedObject.BlockPreviewObject.SetActive(true);
            }
            
            return unusedObject.BlockPreviewObject;
            
            #region Internal
            
            BlockPreviewObject CreatePreviewObject(BlockId id)
            {
                var previewBlock = PreviewBlockCreator.Create(id);
                previewBlock.transform.SetParent(_parentTransform);
                previewBlock.transform.localPosition = Vector3.zero;
                
                return previewBlock;
            }
            
            #endregion
        }
        
        
        public void AllUnUse()
        {
            foreach (var previewObjects in _blockPreviewObjects.Values)
            {
                foreach (var previewObject in previewObjects)
                {
                    previewObject.IsUsed = false;
                    previewObject.BlockPreviewObject.SetActive(false);
                }
            }
        }
        
        public void AllDestroy()
        {
            foreach (var previewObjects in _blockPreviewObjects.Values)
            {
                foreach (var previewObject in previewObjects)
                {
                    previewObject.BlockPreviewObject.Destroy();
                }
            }
            
            _blockPreviewObjects.Clear();
        }
    }
}
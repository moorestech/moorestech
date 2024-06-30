using System.Collections.Generic;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public class BlockPlacePreviewObjectPool
    {
        private readonly Transform _parentTransform;
        private readonly Dictionary<int, List<PreviewObject>> _blockPreviewObjects = new();
        
        public BlockPlacePreviewObjectPool(Transform parentTransform)
        {
            _parentTransform = parentTransform;
        }
        
        class PreviewObject
        {
            public BlockPreviewObject BlockPreviewObject;
            public bool IsUsed;
        }
        
        public BlockPreviewObject GetObject(int blockId)
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
            
            BlockPreviewObject CreatePreviewObject(int id)
            {
                var previewBlock = ClientContext.BlockGameObjectContainer.CreatePreviewBlock(id);
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
                    Object.Destroy(previewObject.BlockPreviewObject);
                }
            }
            
            _blockPreviewObjects.Clear();
        }
    }
}
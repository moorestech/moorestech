using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.Chunk
{
    public class ChunkBlockGameObjectDataStore : MonoBehaviour
    {
        private BlockObjects _blockObjects;
        
        private Dictionary<Vector2Int,BlockGameObject> _blockObjectsDictionary = new();

        [Inject]
        public void Construct(BlockObjects blockObjects)
        {
            _blockObjects = blockObjects;
        }
        
        public void GameObjectBlockPlace(Vector2Int blockPosition, int blockId)
        {
            //すでにブロックがある場合はそっちのブロックに置き換える
            if (_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                Destroy(_blockObjectsDictionary[blockPosition]);
                _blockObjectsDictionary.Remove(blockPosition);
            }
            
            //新しいブロックを設置
            var block = Instantiate(
                _blockObjects.GetBlock(blockId),
                new Vector3(blockPosition.x, 0, blockPosition.y), Quaternion.identity,
                transform);
            _blockObjectsDictionary.Add(blockPosition,block.GetComponent<BlockGameObject>());
        }

        public void GameObjectBlockRemove(Vector2Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;
            Destroy(_blockObjectsDictionary[blockPosition]);
            _blockObjectsDictionary.Remove(blockPosition);
        }
    }
}

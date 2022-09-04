using System.Collections.Generic;
using MainGame.Basic;
using MainGame.ModLoader.Glb;
using MainGame.UnityView.Block;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.Chunk
{
    public class ChunkBlockGameObjectDataStore : MonoBehaviour
    {
        private BlockObjects _blockObjects;
        
        public IReadOnlyDictionary<Vector2Int,BlockGameObject> BlockGameObjectDictionary => _blockObjectsDictionary;
        private readonly Dictionary<Vector2Int,BlockGameObject> _blockObjectsDictionary = new();

        [Inject]
        public void Construct(BlockObjects blockObjects)
        {
            _blockObjects = blockObjects;
        }

        
        public BlockGameObject GetBlockGameObject(Vector2Int position) { return _blockObjectsDictionary.ContainsKey(position) ? _blockObjectsDictionary[position] : null; }
        public bool ContainsBlockGameObject(Vector2Int position) { return _blockObjectsDictionary.ContainsKey(position); }
        
        
        public void GameObjectBlockPlace(Vector2Int blockPosition, int blockId,BlockDirection blockDirection)
        {
            //すでにブロックがあり、IDが違う場合は新しいブロックに置き換えるために削除する
            if (_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                //IDが同じ時は再設置の必要がないため処理を終了
                if (_blockObjectsDictionary[blockPosition].BlockId == blockId)return;
                
                //IDが違うため削除
                Destroy(_blockObjectsDictionary[blockPosition].gameObject);
                _blockObjectsDictionary.Remove(blockPosition);
            }

                
            //新しいブロックを設置
            var pos = new Vector3(blockPosition.x, 0, blockPosition.y);
            var rot = BlockDirectionAngle.GetRotation(blockDirection);
            var block = _blockObjects.CreateBlock(blockId,pos,rot,transform);
                
            _blockObjectsDictionary.Add(blockPosition,block);
        }

        public void GameObjectBlockRemove(Vector2Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                return;
            }
            
            Destroy(_blockObjectsDictionary[blockPosition].gameObject);
            _blockObjectsDictionary.Remove(blockPosition);
        }
    }
}

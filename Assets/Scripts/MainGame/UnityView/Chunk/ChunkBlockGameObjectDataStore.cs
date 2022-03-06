using System.Collections.Generic;
using MainGame.Basic;
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
        
        public void GameObjectBlockPlace(Vector2Int blockPosition, int blockId,BlockDirection blockDirection)
        {
            MainThreadExecutionQueue.Instance.Insert(() =>
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
                var block = Instantiate(
                    _blockObjects.GetBlock(blockId),
                    new Vector3(blockPosition.x, 0, blockPosition.y),
                    BlockDirectionAngle.GetRotation(blockDirection),
                    transform).GetComponent<BlockGameObject>();
                
                //IDを再設定
                block.Construct(blockId);
                _blockObjectsDictionary.Add(blockPosition,block);
            });
        }

        public void GameObjectBlockRemove(Vector2Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                return;
            }
            
            MainThreadExecutionQueue.Instance.Insert(() =>
            {
                Destroy(_blockObjectsDictionary[blockPosition].gameObject);
                _blockObjectsDictionary.Remove(blockPosition);
            });
        }
    }
}

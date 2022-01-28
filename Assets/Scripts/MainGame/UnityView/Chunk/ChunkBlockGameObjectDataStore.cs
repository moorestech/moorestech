using System.Collections.Generic;
using MainGame.UnityView.Interface;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.Chunk
{
    public class ChunkBlockGameObjectDataStore : MonoBehaviour
    {
        private BlockObjects _blockObjects;
        
        private Dictionary<Vector2Int,GameObject> _blockObjectsDictionary = new Dictionary<Vector2Int, GameObject>();

        [Inject]
        public void Construct(IPlaceBlockGameObject placeBlockGameObject,BlockObjects blockObjects)
        {
            _blockObjects = blockObjects;
            placeBlockGameObject.Subscribe(OnBlockPlaceEvent,OnBlockRemoveEvent);
        }


        private void OnBlockPlaceEvent(Vector2Int blockPosition, int blockId)
        {
            //すでにブロックがある場合はそっちのブロックに置き換える
            if (_blockObjectsDictionary.ContainsKey(blockPosition))
            {
                Destroy(_blockObjectsDictionary[blockPosition]);
                _blockObjectsDictionary.Remove(blockPosition);
            }
            
            //新しいブロックを設置
            var block = Instantiate(_blockObjects.GetBlock(blockId),
                new Vector3(blockPosition.x, 0, blockPosition.y), Quaternion.identity);
            _blockObjectsDictionary.Add(blockPosition,block);
        }

        private void OnBlockRemoveEvent(Vector2Int blockPosition)
        {
            //すでにブロックが置かれている時のみブロックを削除する
            if (!_blockObjectsDictionary.ContainsKey(blockPosition)) return;
            Destroy(_blockObjectsDictionary[blockPosition]);
            _blockObjectsDictionary.Remove(blockPosition);
        }
    }
}

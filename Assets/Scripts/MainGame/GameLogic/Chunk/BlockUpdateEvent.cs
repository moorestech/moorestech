using MainGame.Constant;
using MainGame.UnityView.Interface;
using UnityEngine;
using static MainGame.UnityView.Interface.IBlockUpdateEvent;

namespace MainGame.GameLogic.Chunk
{
    public class BlockUpdateEvent : IBlockUpdateEvent
    {
        private event OnBlockPlaceEvent OnBlockPlace;
        private event OnBlockRemoveEvent OnBlockRemove;
        
        
        public void Subscribe(OnBlockPlaceEvent onBlockPlaceEvent, OnBlockRemoveEvent onBlockRemoveEvent)
        {
            OnBlockPlace += onBlockPlaceEvent;
            OnBlockRemove += onBlockRemoveEvent;
        }

        public void UnSubscribe(OnBlockPlaceEvent onBlockPlaceEvent, OnBlockRemoveEvent onBlockRemoveEvent)
        {
            OnBlockPlace -= onBlockPlaceEvent;
            OnBlockRemove -= onBlockRemoveEvent;
        }

        internal void OnOnBlockRemove(Vector2Int blockPosition)
        {
        }

        internal void OnBlockUpdate(Vector2Int blockPosition,int blockId)
        {
            if (blockId == BlockConstant.NullBlockId)
            {
                OnBlockRemove?.Invoke(blockPosition);
                return;
            }
            OnBlockPlace?.Invoke(blockPosition, blockId);
        }
        
        private int[,] _nullBlockIds = new int[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];
        /// <summary>
        /// IDが違う時だけイベントを発火する
        /// </summary>
        internal void DiffChunkUpdate(Vector2Int chunkPos,int[,] newBlockIds,int[,] oldBlockIds = null)
        {
            oldBlockIds ??= _nullBlockIds;
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    if (newBlockIds[i,j] == oldBlockIds[i,j]) continue;
                    //IDが違うのでイベントを発火する
                    var pos = new Vector2Int(chunkPos.x + i, chunkPos.y + j);
                    if (newBlockIds[i,j] == BlockConstant.NullBlockId)
                    {
                        OnBlockRemove?.Invoke(pos);
                        return;
                    }
                    OnBlockPlace?.Invoke(pos, newBlockIds[i,j]);
                }
            }
        }
    }
}
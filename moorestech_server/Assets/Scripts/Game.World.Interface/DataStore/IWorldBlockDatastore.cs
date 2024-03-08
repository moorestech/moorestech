using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockDatastore
    {
        public bool AddBlock(IBlock block, Vector2Int pos, BlockDirection blockDirection);
        public bool RemoveBlock(Vector2Int pos);
        public IBlock GetBlock(Vector2Int pos);
        public WorldBlockData GetOriginPosBlock(Vector2Int pos);
        public bool Exists(Vector2Int pos);
        public bool TryGetBlock(Vector2Int pos, out IBlock block);
        public Vector2Int GetBlockPosition(int entityId);
        public BlockDirection GetBlockDirection(Vector2Int pos);
        public List<SaveBlockData> GetSaveBlockDataList();
        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList);

        public bool ExistsComponentBlock<TComponent>(Vector2Int pos);
        public TComponent GetBlock<TComponent>(Vector2Int pos);
        public bool TryGetBlock<TComponent>(Vector2Int pos, out TComponent component);

        public event Action<(ChangedBlockState state, IBlock block, Vector2Int pos)> OnBlockStateChange;
    }
}
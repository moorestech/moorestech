using System;
using System.Collections.Generic;
using Game.Block.Base;
using Game.Block.Interface;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockDatastore
    {
        public bool AddBlock(IBlock block, Vector3Int pos, BlockDirection blockDirection);
        public bool RemoveBlock(Vector3Int pos);
        public IBlock GetBlock(Vector3Int pos);
        public WorldBlockData GetOriginPosBlock(Vector3Int pos);
        public bool Exists(Vector3Int pos);
        public bool TryGetBlock(Vector3Int pos, out IBlock block);
        public Vector3Int GetBlockPosition(int entityId);
        public BlockDirection GetBlockDirection(Vector3Int pos);
        public List<SaveBlockData> GetSaveBlockDataList();
        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList);

        public bool ExistsComponentBlock<TComponent>(Vector3Int pos);
        public TComponent GetBlock<TComponent>(Vector3Int pos);
        public bool TryGetBlock<TComponent>(Vector3Int pos, out TComponent component);

        public event Action<(ChangedBlockState state, IBlock block, Vector3Int pos)> OnBlockStateChange;
    }
}
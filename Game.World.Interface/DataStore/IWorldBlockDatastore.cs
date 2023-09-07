using System;
using System.Collections.Generic;
using Core.Block;
using Core.Block.Blocks;
using Core.Block.Blocks.State;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockDatastore
    {
        public bool AddBlock(IBlock block, int x, int y, BlockDirection blockDirection);
        public bool RemoveBlock(int x, int y);
        public IBlock GetBlock(int x, int y);
        public bool Exists(int x, int y);
        public bool TryGetBlock(int x, int y, out IBlock block);
        public (int,int) GetBlockPosition(int entityId);
        public BlockDirection GetBlockDirection(int x, int y);
        public List<SaveBlockData> GetSaveBlockDataList();
        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList);

        public bool ExistsComponentBlock<TComponent>(int x, int y);
        public TComponent GetBlock<TComponent>(int x, int y);
        public bool TryGetBlock<TComponent>(int x, int y, out TComponent component);

        public event Action<(ChangedBlockState state, IBlock block, int x, int y)> OnBlockStateChange;
    }
}
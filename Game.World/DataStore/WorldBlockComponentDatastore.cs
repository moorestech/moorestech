using System;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace World.DataStore
{
    public class WorldBlockComponentDatastore<T> : IWorldBlockComponentDatastore<T>
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public WorldBlockComponentDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public bool ExistsComponentBlock(int x, int y)
        {
            return _worldBlockDatastore.GetBlock(x, y) is T;
        }

        public T GetBlock(int x, int y)
        {
            var block = _worldBlockDatastore.GetBlock(x, y);
            if (block is T electric)
            {
                return electric;
            }
            throw new Exception("Block is not " + typeof(T).ToString());
        }
    }
}
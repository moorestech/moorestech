using Core.Ore;
using Game.World.Interface.DataStore;

namespace Game.WorldMap
{
    /// <summary>
    /// そのintIdのブロックの直下にあるoreを返す
    /// </summary>
    public class CheckOreMining : ICheckOreMining
    {
        private IWorldBlockDatastore _worldBlockDatastore;
        private VeinGenerator _veinGenerator;

        public CheckOreMining(IWorldBlockDatastore worldBlockDatastore, VeinGenerator veinGenerator)
        {
            _worldBlockDatastore = worldBlockDatastore;
            _veinGenerator = veinGenerator;
        }

        public int Check(int intId)
        {
            var (x,y) = _worldBlockDatastore.GetBlockPosition(intId);
            return _veinGenerator.GetOreId(x, y);
        }
    }
}
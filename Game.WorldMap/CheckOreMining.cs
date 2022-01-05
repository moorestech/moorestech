using Core.Ore;

namespace Game.WorldMap
{
    /// <summary>
    /// そのintIdのブロックの直下にあるoreを返す
    /// </summary>
    public class CheckOreMining : ICheckOreMining
    {
        public int Check(int intId)
        {
            //TODO ore configの実装をする
            //TODO とりあえず常にironを返す
            return 1;
        }
    }
}
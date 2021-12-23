using Game.World.Interface.DataStore;

namespace Game.World.Interface.Util
{
    public static class CoordinateCreator
    {
        public static Coordinate New(int x,int y)
        {
            return new Coordinate()
            {
                X = x,
                Y = y
            };
        }
    }
}
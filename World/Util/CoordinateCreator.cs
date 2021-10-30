namespace World.Util
{
    public static class CoordinateCreator
    {
        public static Coordinate New(int X,int Y)
        {
            return new Coordinate()
            {
                x = X,
                y = Y
            };
        }
    }
}
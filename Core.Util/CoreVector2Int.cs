namespace Core.Util
{
    public struct CoreVector2Int
    {
        public int X;
        public int Y;

        public CoreVector2Int(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public static bool operator ==(CoreVector2Int a, CoreVector2Int b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(CoreVector2Int a, CoreVector2Int b)
        {
            return !(a == b);
        }
    }
}
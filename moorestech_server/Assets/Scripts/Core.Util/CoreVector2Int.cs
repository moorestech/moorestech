namespace Core.Util
{
    public readonly struct CoreVector2Int
    {
        public readonly int X;
        public readonly int Y;

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
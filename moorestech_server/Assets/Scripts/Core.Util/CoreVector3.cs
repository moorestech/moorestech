namespace Core.Util
{
    public readonly struct CoreVector3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public CoreVector3(float x = 0, float y = 0, float z = 0)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
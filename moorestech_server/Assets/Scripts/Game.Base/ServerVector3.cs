namespace Game.Base
{
    public readonly struct ServerVector3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public ServerVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
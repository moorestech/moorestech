namespace Core.BeltTransport
{
    public readonly struct BeltNetworkCell
    {
        public readonly int Id, X, Y, Z, Speed;
        public readonly BeltDirection Forward;
        public readonly string SpeedProfile;
        public readonly BeltCellSurfaceProfile Surface;

        public BeltNetworkCell(int id, int x, int y, int z, int speed, string speedProfile, BeltDirection forward, BeltCellSurfaceProfile surface)
        {
            Id = id; X = x; Y = y; Z = z;
            Speed = speed; SpeedProfile = speedProfile; Forward = forward; Surface = surface;
        }

        public BeltNetworkCell WithSpeed(int speed) => new BeltNetworkCell(Id, X, Y, Z, speed, SpeedProfile, Forward, Surface);
    }
}

using System;
using Core.BeltTransport;
using MessagePack;

namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltCellMessagePack
    {
        [Key(0)] public int Id { get; }
        [Key(1)] public int X { get; }
        [Key(2)] public int Y { get; }
        [Key(3)] public int Z { get; }
        [Key(4)] public int Speed { get; }
        [Key(5)] public string SpeedProfile { get; }
        [Key(6)] public BeltDirection Forward { get; }

        [SerializationConstructor]
        public BeltCellMessagePack(int id, int x, int y, int z, int speed, string speedProfile, BeltDirection forward)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
            Speed = speed;
            SpeedProfile = speedProfile;
            Forward = forward;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltCellMessagePack() { }

        public BeltCellMessagePack(BeltNetworkCell value)
        {
            Id = value.Id;
            X = value.X;
            Y = value.Y;
            Z = value.Z;
            Speed = value.Speed;
            SpeedProfile = value.SpeedProfile;
            Forward = value.Forward;
        }

        public BeltNetworkCell ToCore() => new(Id, X, Y, Z, Speed, SpeedProfile, Forward);
    }
}

using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    public readonly struct TrainCarContext
    {
        public bool HasSnapshot { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public double CurrentSpeed { get; }
        public int MasconLevel { get; }
        public bool IsFacingForward { get; }

        private TrainCarContext(bool hasSnapshot, Vector3 position, Quaternion rotation, double currentSpeed, int masconLevel, bool isFacingForward)
        {
            HasSnapshot = hasSnapshot;
            Position = position;
            Rotation = rotation;
            CurrentSpeed = currentSpeed;
            MasconLevel = masconLevel;
            IsFacingForward = isFacingForward;
        }

        public static TrainCarContext CreateAvailable(Vector3 position, Quaternion rotation, double currentSpeed, int masconLevel, bool isFacingForward)
        {
            return new TrainCarContext(true, position, rotation, currentSpeed, masconLevel, isFacingForward);
        }

        public static TrainCarContext CreateUnavailable()
        {
            return new TrainCarContext(false, default, Quaternion.identity, 0.0, 0, true);
        }
    }
}

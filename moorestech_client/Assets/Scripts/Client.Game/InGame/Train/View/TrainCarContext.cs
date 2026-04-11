namespace Client.Game.InGame.Train.View
{
    public readonly struct TrainCarContext
    {
        public bool HasSnapshot { get; }
        public double CurrentSpeed { get; }
        public int MasconLevel { get; }

        private TrainCarContext(bool hasSnapshot, double currentSpeed, int masconLevel)
        {
            HasSnapshot = hasSnapshot;
            CurrentSpeed = currentSpeed;
            MasconLevel = masconLevel;
        }

        public static TrainCarContext CreateAvailable(double currentSpeed, int masconLevel)
        {
            return new TrainCarContext(true, currentSpeed, masconLevel);
        }

        public static TrainCarContext CreateUnavailable()
        {
            return new TrainCarContext(false, 0.0, 0);
        }
    }
}

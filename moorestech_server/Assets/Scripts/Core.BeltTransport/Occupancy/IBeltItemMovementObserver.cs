namespace Core.BeltTransport
{
    internal interface IBeltItemMovementObserver
    {
        void Insert(int distance);
        void Remove(int distance);
        void Move(int before, int after);
    }

    // 単独segmentはネットワークのセルを所有しない。
    // Standalone segments do not own network cells.
    internal sealed class UntrackedBeltMovement : IBeltItemMovementObserver
    {
        internal static readonly UntrackedBeltMovement Instance = new UntrackedBeltMovement();
        private UntrackedBeltMovement() { }
        public void Insert(int distance) { }
        public void Remove(int distance) { }
        public void Move(int before, int after) { }
    }
}

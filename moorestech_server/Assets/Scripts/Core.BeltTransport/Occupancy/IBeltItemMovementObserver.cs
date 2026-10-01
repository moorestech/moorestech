namespace Core.BeltTransport
{
    internal interface IBeltItemMovementObserver
    {
        void Insert(int distance);
        void Remove(int distance);
        void Move(int before, int after);
    }

}

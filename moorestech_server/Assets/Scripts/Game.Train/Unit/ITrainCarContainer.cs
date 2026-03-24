namespace Game.Train.Unit
{
    public interface ITrainCarContainer
    {
        int GetWeight();
        bool IsFull();
        bool IsEmpty();
    }
}
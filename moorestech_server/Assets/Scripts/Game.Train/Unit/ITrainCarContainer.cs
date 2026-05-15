namespace Game.Train.Unit
{
    public interface ITrainCarContainer
    {
        int GetWeight();
        bool IsFull();
        bool IsEmpty();

        // 自分が列車に装着された/外された通知。
        // Notification of being attached to/detached from a train.
        void OnAttachedToCar(TrainCar trainCar);
        void OnDetachedFromCar();
    }
}
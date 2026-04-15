namespace Game.Train.Unit
{
    public interface ITrainUnitMutationDatastore
    {
        void RegisterTrain(TrainUnit trainUnit);
        void UnregisterTrain(TrainUnit trainUnit);
        void RebuildCarToUnitIndex();
    }
}

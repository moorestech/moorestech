namespace Game.Train.Unit
{
    public interface IFuelProviderTrainCarContainer : ITrainCarContainer
    {
        /// <summary>
        ///     燃料を消費する
        ///     Consume fuel from the train car container
        /// </summary>
        /// <returns>
        ///     消費にかかる時間
        ///     Time required for consumption
        /// </returns>
        float ConsumeFuel(TrainCar trainCar);
    }
}
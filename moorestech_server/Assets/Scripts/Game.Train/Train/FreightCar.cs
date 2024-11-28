namespace Game.Train.Train
{
    public class FreightCar : ITrainCar
    {
        public TrainCarType TrainCarType => TrainCarType.Freight;
    }
}
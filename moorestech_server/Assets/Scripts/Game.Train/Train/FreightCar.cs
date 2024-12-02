namespace Game.Train.Train
{
    public class FreightCar : TrainCarBase, ITrainCar
    {
        public TrainCarType TrainCarType => TrainCarType.Freight;
    }
}
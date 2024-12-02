using Game.Train.Train;

namespace Game.Train.Train
{
    public class LocomotiveCar : TrainCarBase , ITrainCar
    {
        public TrainCarType TrainCarType => TrainCarType.Locomotive;
    }
    

}


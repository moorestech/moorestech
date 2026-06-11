using Client.Game.InGame.Train.View;

namespace Client.Game.InGame.Train.View.Object
{
    public interface ITrainCarObjectProcessor
    {
        public void Initialize(TrainCarEntityObject trainCarEntityObject);
        public void Update(TrainCarContext context);
    }
}

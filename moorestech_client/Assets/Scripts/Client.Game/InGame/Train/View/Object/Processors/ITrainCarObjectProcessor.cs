using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;

namespace Client.Game.InGame.Train.View.Object.Processors
{
    public interface ITrainCarObjectProcessor
    {
        public void Initialize(TrainCarEntityObject trainCarEntityObject);
        public void Update(TrainCarContext context);
    }
}

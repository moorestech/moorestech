using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;

namespace Client.Game.InGame.Train.View.Object.Processors
{
    public interface ITrainCarObjectProcessor
    {
        public void Initialize(TrainCarEntityObject trainCarEntityObject);

        // MonoBehaviour実装があるのでUnityのUpdateと名前を分ける。衝突すると毎ロード「Update() can not take parameters」が出る
        // Implementations are MonoBehaviours, so the name must differ from Unity's Update; colliding logs "Update() can not take parameters" on every load
        public void ManualUpdate(TrainCarContext context);
    }
}

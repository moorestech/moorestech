using Game.Train.Unit;

namespace Client.Game.InGame.Train.View.Object.Core
{
    /// <summary>
    ///     車両IDから今の表示を引く読み取り口
    ///     Read-only lookup from a car ID to its current view
    /// </summary>
    public interface ITrainCarViewRegistry
    {
        bool TryGetEntity(TrainCarInstanceId id, out TrainCarEntityObject entity);
    }
}

using Game.Train.Unit;

namespace Client.Network.API.Requests.Removal
{
    // 車両キューの送信境界。完了は既存イベントが通知する
    // Send boundary for the car queue; existing events report completion
    public interface ITrainCarRemovalSender
    {
        void RemoveTrain(TrainCarInstanceId id);
    }
}

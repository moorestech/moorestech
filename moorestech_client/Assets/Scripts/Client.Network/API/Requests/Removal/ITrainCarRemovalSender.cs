using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;

namespace Client.Network.API.Requests.Removal
{
    // 車両撤去の送信境界。拒否は要求ごとの応答で確定する
    // Send boundary for car removal; each request is settled by its own response
    public interface ITrainCarRemovalSender
    {
        UniTask<RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack> RemoveTrain(TrainCarInstanceId id, CancellationToken ct);
    }
}

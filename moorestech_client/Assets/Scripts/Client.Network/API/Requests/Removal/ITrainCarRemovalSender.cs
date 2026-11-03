using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;

namespace Client.Network.API.Requests.Removal
{
    // 車両撤去の送信境界。応答待ちは共通の応答期限で必ず終わる
    // Send boundary for car removal; the wait always ends within the shared response window
    public interface ITrainCarRemovalSender
    {
        UniTask<(RemoveTrainCarProtocol.RemoveTrainCarResponseMessagePack response, PacketWaitCompletionReason reason)> RemoveTrain(TrainCarInstanceId id, CancellationToken ct);
    }
}

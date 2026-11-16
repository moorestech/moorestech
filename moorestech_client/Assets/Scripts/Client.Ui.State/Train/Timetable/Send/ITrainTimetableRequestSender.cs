using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.Train.Timetable.Send
{
    /// <summary>
    /// 列車の時刻表をサーバーへ取り寄せる口（応答なしは null）
    /// Port requesting a train's timetable from the server (null when no response arrives)
    /// </summary>
    public interface ITrainTimetableRequestSender
    {
        UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct);
    }
}

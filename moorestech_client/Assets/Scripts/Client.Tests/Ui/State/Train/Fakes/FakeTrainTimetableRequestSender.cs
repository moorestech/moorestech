using System.Collections.Generic;
using System.Threading;
using Client.Ui.State.Train.Timetable.Send;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 時刻表の取り寄せを数えるだけの偽物。応答は返さず保留のまま（取得中のラッチを見るテスト用）
    /// Fake that only counts timetable requests; responses stay pending (for tests that inspect the in-flight latch)
    /// </summary>
    public sealed class FakeTrainTimetableRequestSender : ITrainTimetableRequestSender
    {
        public List<TrainUnitInstanceId> SentTrainUnitInstanceIds { get; } = new();

        public UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
        {
            SentTrainUnitInstanceIds.Add(trainUnitInstanceId);
            return new UniTaskCompletionSource<GetTrainTimetableProtocol.GetTrainTimetableResponse>().Task;
        }
    }
}

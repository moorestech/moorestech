using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.Train.Timetable.Send
{
    /// <summary>
    /// 本番の時刻表の取り寄せ（ClientContext の応答付き API）
    /// Production timetable request via the ClientContext request-response API
    /// </summary>
    public sealed class VanillaApiTrainTimetableRequestSender : ITrainTimetableRequestSender
    {
        public UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.Train.GetTrainTimetable(trainUnitInstanceId, ct);
        }
    }
}

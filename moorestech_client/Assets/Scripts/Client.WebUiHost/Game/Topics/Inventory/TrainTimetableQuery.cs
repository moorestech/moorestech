using System.Threading;
using Client.Game.InGame.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics
{
    // 時刻表の問い合わせ口。取得の要求回数を境界の外から観測できるよう切り出す
    // The timetable query boundary, split out so request counts are observable from outside
    public interface ITrainTimetableQuery
    {
        UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct);
    }

    // 本番はサーバーへ va:getTrainTimetable を送る
    // In production, send va:getTrainTimetable to the server
    public class VanillaApiTrainTimetableQuery : ITrainTimetableQuery
    {
        public UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
        {
            return ClientContext.VanillaApi.Response.GetTrainTimetable(trainUnitInstanceId, ct);
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// 時刻表の取得依頼・置換・自動運転をサーバーへ送る（規則は Web の TrainTimetableActions の写し）
    /// Sends timetable fetch requests, replaces and auto-run toggles to the server (rules are a port of Web TrainTimetableActions)
    /// </summary>
    public sealed class TrainTimetableCommands : ITrainTimetableCommands
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;
        private readonly TrainTimetableFetcher _fetcher;

        public TrainTimetableCommands(SubInventoryState subInventoryState, TrainUnitClientCache cache, TrainTimetableFetcher fetcher)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
            _fetcher = fetcher;
        }

        public void RequestOpen()
        {
            // 列車が引けなければ「列車なしで開いた」と記録する（Web の TrainTimetableOpenActionHandler の写し）
            // Without a resolvable train, record the tab as opened without one (port of Web TrainTimetableOpenActionHandler)
            if (OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) _fetcher.RequestForOpenedTab(trainUnitId);
            else _fetcher.MarkTabOpenedWithoutTrain();
        }

        public async UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);

            // 応答はネットワーク由来なので null（切断・時間切れ）を拒否として扱う
            // The response comes from the network, so null (disconnect or timeout) counts as a refusal
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(trainUnitId, TrainTimetableDeparture.ToServerStops(stops));
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainTimetableReplaceFailed, response == null ? "no response" : response.FailureReason.ToString());
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetAutoRun(bool enabled)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);

            // 受理されても求めた値で適用されていなければ拒否（Web の写しの照合）
            // Even when accepted, refuse if the applied value differs from the request (check ported from Web)
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(trainUnitId, enabled);
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainAutoRunFailed, response == null ? "no response" : response.FailureReason.ToString());
            if (response.AppliedIsAutoRun == enabled) return UiCommandAccepted.Instance;

            // 適用値は wire でなく診断へ
            // Applied value goes to diagnostic, not the wire
            return UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainAutoRunNotApplied, $"applied {response.AppliedIsAutoRun}, requested {enabled}");
        }
    }
}

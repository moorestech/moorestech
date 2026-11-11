using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Ui.State.Train.TimetableEdit.Send
{
    /// <summary>
    /// 時刻表の取得依頼・置換・自動運転をサーバーへ送る（Web ホスト TrainTimetableActions と同じ規則。D-2）
    /// Sends timetable fetch requests, replaces and auto-run toggles to the server (same rules as the Web host TrainTimetableActions; D-2)
    /// </summary>
    public sealed class TrainTimetableCommands : ITrainTimetableCommands
    {
        private const string LogTag = "TrainTimetableCommands";
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
            // 列車が引けなければ「列車なしで開いた」と記録する（Web ホスト TrainTimetableOpenActionHandler と同じ State の口）
            // Without a resolvable train, record the tab as opened without one (the same State calls as the Web host TrainTimetableOpenActionHandler)
            if (OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) _fetcher.RequestForOpenedTab(trainUnitId);
            else _fetcher.MarkTabOpenedWithoutTrain();
        }

        public async UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);

            // 応答はネットワーク由来なので null（切断・時間切れ）を拒否として扱う
            // The response comes from the network, so null (disconnect or timeout) counts as a refusal
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(trainUnitId, TrainTimetableDeparture.ToServerStops(stops));
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.TrainTimetableReplaceFailed, response == null ? string.Empty : response.FailureReason.ToString());
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetAutoRun(bool enabled)
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.BlockNotOpen, string.Empty);

            // 受理されても求めた値で適用されていなければ拒否（Web ホストと同じ照合）
            // Even when accepted, refuse if the applied value differs from the request (same check as the Web host)
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(trainUnitId, enabled);
            var response = await ClientContext.VanillaApi.Response.Train.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.TrainAutoRunFailed, response == null ? string.Empty : response.FailureReason.ToString());
            if (response.AppliedIsAutoRun == enabled) return UiCommandAccepted.Instance;

            // 適用された値はサーバーの失敗理由ではないので Detail に入れず、ログにだけ残す（Web ホストの set_auto_run_not_applied:<値> と同じ情報）
            // The applied value is not a server failure reason, so it stays out of Detail and goes to the log only (same information as the Web host's set_auto_run_not_applied:<value>)
            Debug.LogWarning($"[{LogTag}] auto-run applied as {response.AppliedIsAutoRun}, requested {enabled}");
            return TrainCommandRejection.Reject(LogTag, UiCommandRejectReason.TrainAutoRunNotApplied, string.Empty);
        }
    }
}

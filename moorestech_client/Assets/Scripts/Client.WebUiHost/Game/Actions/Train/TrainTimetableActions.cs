using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Network.API;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using Newtonsoft.Json.Linq;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 開いている列車の時刻表を丸ごと置き換える
    // Replace the whole timetable of the open train
    public class TrainTimetableReplaceActionHandler : IActionHandler
    {
        public string ActionType => "train_timetable.replace";
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;

        public TrainTimetableReplaceActionHandler(SubInventoryState subInventoryState, TrainUnitClientCache cache)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["stops"] is not JArray stopTokens) return TrainTimetableActionSupport.Reject("invalid_payload");
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainTimetableActionSupport.Reject("train_not_open");

            // 1件でも不正な停車駅があれば置換全体を拒否する
            // Reject the whole replacement if any stop is malformed
            var stops = new List<TrainTimetableStop>(stopTokens.Count);
            foreach (var token in stopTokens)
            {
                if (!TrainTimetableStopParser.TryParse(token, out var stop))
                    return TrainTimetableActionSupport.Reject("invalid_stop");
                stops.Add(stop);
            }

            // 検証済みの編集だけをサーバーへ送る
            // Send only validated edits to the server
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateReplaceTimetableRequest(trainUnitId, stops);
            var response = await ClientContext.VanillaApi.Response.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainTimetableActionSupport.Reject($"replace_failed:{response?.FailureReason}");
            return ActionResult.Success();
        }
    }

    // 開いている列車の自動運転をON/OFFする
    // Toggle auto-run for the open train
    public class TrainTimetableSetAutoRunActionHandler : IActionHandler
    {
        public string ActionType => "train_timetable.set_auto_run";
        private readonly SubInventoryState _subInventoryState;
        private readonly TrainUnitClientCache _cache;

        public TrainTimetableSetAutoRunActionHandler(SubInventoryState subInventoryState, TrainUnitClientCache cache)
        {
            _subInventoryState = subInventoryState;
            _cache = cache;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["enabled"] is not JValue { Type: JTokenType.Boolean } enabled) return TrainTimetableActionSupport.Reject("invalid_payload");
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _cache, out var trainUnitId)) return TrainTimetableActionSupport.Reject("train_not_open");

            // 検証済みの編集だけをサーバーへ送る
            // Send only validated edits to the server
            var request = TrainScheduleEditProtocol.TrainScheduleEditRequest.CreateSetAutoRunRequest(trainUnitId, (bool)enabled);
            var response = await ClientContext.VanillaApi.Response.SendTrainScheduleEdit(request, CancellationToken.None);
            if (response == null || !response.Success) return TrainTimetableActionSupport.Reject($"set_auto_run_failed:{response?.FailureReason}");
            return ActionResult.Success();
        }
    }

    // 時刻表actionの拒否理由をログへ残して失敗を返す
    // Log the timetable action's rejection reason and return a failure
    internal static class TrainTimetableActionSupport
    {
        public static ActionResult Reject(string reason)
        {
            Debug.LogWarning($"[TrainTimetableAction] rejected: {reason}");
            return ActionResult.Fail(reason);
        }
    }
}

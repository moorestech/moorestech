using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    // 時刻表タブで開いた列車が変わったときだけ、サーバーから現在の時刻表を取り寄せる
    // Fetch the current timetable from the server only when the train opened in the timetable tab changes
    internal class TrainTimetableFetcher
    {
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly ClientTrainTimetableDatastore _timetables;
        private TrainUnitInstanceId? _requestedTrainUnitInstanceId;

        public TrainTimetableFetcher(TrainUnitClientCache trainUnitClientCache, ClientTrainTimetableDatastore timetables)
        {
            _trainUnitClientCache = trainUnitClientCache;
            _timetables = timetables;
        }

        public void RequestWhenTrainChanged(TrainSubInventorySource trainSource)
        {
            // 列車IDが引けない間は取得せず、DTO側の警告に任せる
            // Skip while the train id is unresolved; the DTO builder logs that case
            if (!_trainUnitClientCache.TryGetCarSnapshot(new TrainCarInstanceId(trainSource.TrainCarInstanceId), out var unit, out _, out _, out _)) return;
            var trainUnitInstanceId = unit.TrainUnitInstanceId;
            if (_requestedTrainUnitInstanceId == trainUnitInstanceId) return;
            _requestedTrainUnitInstanceId = trainUnitInstanceId;
            FetchTimetableAsync(trainUnitInstanceId).Forget();
        }

        // 閉じたら忘れ、次に開いたとき取り直す
        // Forget on close so the next open fetches again
        public void Reset()
        {
            _requestedTrainUnitInstanceId = null;
        }

        private async UniTaskVoid FetchTimetableAsync(TrainUnitInstanceId trainUnitInstanceId)
        {
            var response = await ClientContext.VanillaApi.Response.GetTrainTimetable(trainUnitInstanceId, CancellationToken.None);
            if (response == null || !response.Found)
            {
                Debug.LogWarning($"[TrainTimetableFetcher] timetable not found on server: {trainUnitInstanceId}");
                return;
            }
            _timetables.Apply(response.Timetable);
        }
    }
}

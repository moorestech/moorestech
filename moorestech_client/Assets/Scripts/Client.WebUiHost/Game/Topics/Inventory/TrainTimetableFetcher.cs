using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    // 時刻表タブで開いた列車が変わったときだけ、サーバーから現在の時刻表を取り寄せる
    // Fetch the current timetable from the server only when the train opened in the timetable tab changes
    internal class TrainTimetableFetcher
    {
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly IClientTrainTimetableMutator _timetables;
        private readonly Subject<Unit> _onRetryRequested = new();
        // 再配信フィルタ用の開いている列車（閉じるまで保持）と、再要求を抑える取得ラッチ（失敗で解除）は別物
        // The open train for the republish filter (kept until close) differs from the fetch latch (cleared on failure)
        private TrainUnitInstanceId? _openTrainUnitInstanceId;
        private TrainUnitInstanceId? _requestedTrainUnitInstanceId;
        private int _requestGeneration;
        private bool _retriedSinceOpen;
        public IObservable<Unit> OnRetryRequested => _onRetryRequested;

        public TrainTimetableFetcher(TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableMutator timetables)
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
            _openTrainUnitInstanceId = trainUnitInstanceId;
            if (_requestedTrainUnitInstanceId == trainUnitInstanceId) return;
            _requestedTrainUnitInstanceId = trainUnitInstanceId;
            FetchTimetableAsync(trainUnitInstanceId, ++_requestGeneration).Forget();

            #region Internal

            async UniTaskVoid FetchTimetableAsync(TrainUnitInstanceId id, int generation)
            {
                var response = await ClientContext.VanillaApi.Response.GetTrainTimetable(id, CancellationToken.None);
                if (response == null)
                {
                    // 応答なしは「列車が無い」と区別し、最新の要求だけラッチを外して再配信経由で取り直す
                    // No response differs from "no train"; only the latest request clears the latch and retries via republish
                    Debug.LogWarning($"[TrainTimetableFetcher] no response for timetable request: {id}");
                    if (generation != _requestGeneration) return;
                    // 決まって失敗する応答で毎フレーム再要求しないよう、再試行は開くたびに1回まで
                    // Retry at most once per open so a deterministic failure does not re-request every frame
                    if (_retriedSinceOpen)
                    {
                        Debug.LogWarning($"[TrainTimetableFetcher] retry already used; waiting for reopen: {id}");
                        return;
                    }
                    _retriedSinceOpen = true;
                    _requestedTrainUnitInstanceId = null;
                    _onRetryRequested.OnNext(Unit.Default);
                    return;
                }
                if (response.Timetable == null)
                {
                    // サーバーに列車が無い場合は再要求せず、要求嵐を防ぐ
                    // No train on the server; keep the latch to avoid a request storm on every publish
                    Debug.LogWarning($"[TrainTimetableFetcher] train not found on server: {id}");
                    return;
                }
                _timetables.Apply(response.Timetable);
            }

            #endregion
        }

        // 閉じたら忘れ、次に開いたとき取り直す
        // Forget on close so the next open fetches again
        public void Reset()
        {
            _openTrainUnitInstanceId = null;
            _requestedTrainUnitInstanceId = null;
            _requestGeneration++;
            _retriedSinceOpen = false;
        }

        // 現在開いている列車の時刻表更新かどうかを再配信フィルタへ渡す
        // Tell the republish filter whether an update belongs to the currently open train
        public bool IsOpenTrain(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _openTrainUnitInstanceId == trainUnitInstanceId;
        }
    }
}

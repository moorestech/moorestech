using System;
using System.Threading;
using Client.Game.InGame.Train.Timetable;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    // 時刻表タブを開いた列車の時刻表をサーバーから取り寄せ、取得できなかった列車を覚える
    // Fetch the timetable of the train whose timetable tab is open, and remember trains that could not be fetched
    public class TrainTimetableFetcher
    {
        private readonly ITrainTimetableQuery _query;
        private readonly IClientTrainTimetableMutator _timetables;
        private readonly Subject<Unit> _onFetchFailed = new();
        // タブを開いた列車（閉じるまで保持）と、再要求を抑える取得ラッチは別物
        // The train whose tab is open (kept until close) differs from the latch that suppresses re-requests
        private TrainUnitInstanceId? _timetableOpenTrainUnitInstanceId;
        private TrainUnitInstanceId? _requestedTrainUnitInstanceId;
        private TrainUnitInstanceId? _unavailableTrainUnitInstanceId;
        private int _requestGeneration;
        private bool _retriedSinceOpen;
        public IObservable<Unit> OnFetchFailed => _onFetchFailed;

        public TrainTimetableFetcher(ITrainTimetableQuery query, IClientTrainTimetableMutator timetables)
        {
            _query = query;
            _timetables = timetables;
        }

        // 時刻表タブが開かれた列車の取得を始める。取得中・取得済みの列車は再要求しない
        // Start fetching for the train whose timetable tab opened; a train already in flight or fetched is not re-requested
        public void RequestForOpenedTab(TrainUnitInstanceId trainUnitInstanceId)
        {
            _timetableOpenTrainUnitInstanceId = trainUnitInstanceId;
            if (_requestedTrainUnitInstanceId == trainUnitInstanceId) return;
            _requestedTrainUnitInstanceId = trainUnitInstanceId;
            _unavailableTrainUnitInstanceId = null;
            FetchTimetableAsync(trainUnitInstanceId, ++_requestGeneration).Forget();
        }

        // 連結・分割で開いている車両の列車が変わったら、タブを開いている間だけ取り直す
        // When coupling or splitting changes the open car's train, refetch only while the tab is open
        public void FollowOpenTrainChange(TrainUnitInstanceId trainUnitInstanceId)
        {
            if (_timetableOpenTrainUnitInstanceId == null) return;
            RequestForOpenedTab(trainUnitInstanceId);
        }

        // サブインベントリの開閉で忘れ、次に開いたとき取り直す
        // Forget on sub-inventory open/close so the next open fetches again
        public void Close()
        {
            _timetableOpenTrainUnitInstanceId = null;
            _requestedTrainUnitInstanceId = null;
            _unavailableTrainUnitInstanceId = null;
            _requestGeneration++;
            _retriedSinceOpen = false;
        }

        public bool IsUnavailable(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _unavailableTrainUnitInstanceId == trainUnitInstanceId;
        }

        private async UniTaskVoid FetchTimetableAsync(TrainUnitInstanceId trainUnitInstanceId, int generation)
        {
            Debug.Log($"[TrainTimetableFetcher] requesting timetable: {trainUnitInstanceId}");
            var response = await _query.GetTrainTimetable(trainUnitInstanceId, CancellationToken.None);
            if (response == null)
            {
                OnNoResponse();
                return;
            }
            if (response.Timetable == null)
            {
                // サーバーに列車が無い場合は再要求せず取得不可として表示する
                // No train on the server: do not re-request, show it as unavailable
                Debug.LogWarning($"[TrainTimetableFetcher] train not found on server: {trainUnitInstanceId}");
                MarkUnavailable();
                return;
            }
            _timetables.Apply(response.Timetable.ToModel());

            #region Internal

            void OnNoResponse()
            {
                // 応答なしは「列車が無い」と区別してログし、最新の要求だけが後続を決める
                // Log no-response separately from "no train"; only the latest request decides what follows
                Debug.LogWarning($"[TrainTimetableFetcher] no response for timetable request: {trainUnitInstanceId}");
                if (generation != _requestGeneration) return;
                // 決まって失敗する応答で要求し続けないよう、再試行は開くたびに1回まで
                // Retry at most once per open so a deterministic failure does not keep requesting
                if (_retriedSinceOpen)
                {
                    Debug.LogWarning($"[TrainTimetableFetcher] retry already used; unavailable until reopen: {trainUnitInstanceId}");
                    MarkUnavailable();
                    return;
                }
                _retriedSinceOpen = true;
                FetchTimetableAsync(trainUnitInstanceId, ++_requestGeneration).Forget();
            }

            void MarkUnavailable()
            {
                if (generation != _requestGeneration) return;
                _unavailableTrainUnitInstanceId = trainUnitInstanceId;
                _onFetchFailed.OnNext(Unit.Default);
            }

            #endregion
        }
    }
}

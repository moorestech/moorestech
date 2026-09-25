using System;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Timetable;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    // 時刻表タブを開いた列車の時刻表を取り寄せ、失敗を覚える
    // Fetch the timetable of the train whose timetable tab is open, and remember trains that could not be fetched
    public class TrainTimetableFetcher
    {
        private readonly IClientTrainTimetableMutator _timetables;
        private readonly IClientTrainTimetableLookup _timetableLookup;
        private readonly Subject<Unit> _onFetchFailed = new();
        // タブが開いている事実と再要求を抑える取得ラッチは別物
        // Whether the tab is open (kept until close) differs from the re-request latch; the tracker owns the open train id
        private bool _isTimetableTabOpen;
        private TrainUnitInstanceId? _requestedTrainUnitInstanceId;
        private TrainUnitInstanceId? _unavailableTrainUnitInstanceId;
        private CancellationTokenSource _cts;
        private int _requestGeneration;
        private bool _retriedForRequestedTrain;
        private bool _isFetchInFlight;
        private IDisposable _timetableUpdatedSubscription;
        public IObservable<Unit> OnFetchFailed => _onFetchFailed;

        public TrainTimetableFetcher(IClientTrainTimetableMutator timetables, IClientTrainTimetableLookup timetableLookup)
        {
            _timetables = timetables;
            _timetableLookup = timetableLookup;
        }

        // 開かれた列車の取得を始める。取得中・取得済みは再要求しない
        // Start fetching for the train whose timetable tab opened; a train already in flight or fetched is not re-requested
        public void RequestForOpenedTab(TrainUnitInstanceId trainUnitInstanceId)
        {
            _isTimetableTabOpen = true;
            // タブが開いている間だけ新着通知を待ち受ける（Closeで切る）
            // Listen for delivered timetables only while the tab is open; Close disposes the subscription
            _timetableUpdatedSubscription ??= _timetableLookup.OnTimetableUpdated.Subscribe(OnTimetableDelivered);
            if (_requestedTrainUnitInstanceId == trainUnitInstanceId) return;
            // 打ち切りは旧列車IDのまま行い、放棄したのがどの取得か判るようにする
            // Cancel while the old train id is still set so the log names the abandoned fetch
            var token = RestartCancellation();
            // 再試行の残りは列車ごとに持つ
            // The retry allowance is per requested train and resets when the train changes
            _requestedTrainUnitInstanceId = trainUnitInstanceId;
            _unavailableTrainUnitInstanceId = null;
            _retriedForRequestedTrain = false;
            _isFetchInFlight = true;
            FetchTimetableAsync(trainUnitInstanceId, ++_requestGeneration, token).Forget();
        }

        // 列車が引けなくてもタブを開いた事実は残す
        // Even if the train cannot be resolved before the car snapshot arrives, keep the tab-open fact for the later fetch
        public void MarkTabOpenedWithoutTrain()
        {
            _isTimetableTabOpen = true;
        }

        // 所属列車が変わったらタブを開いている間だけ取り直す
        // When coupling, splitting, or a late car snapshot changes the open car's train, refetch only while the tab is open
        public void FollowOpenTrainChange(TrainUnitInstanceId trainUnitInstanceId)
        {
            if (!_isTimetableTabOpen) return;
            RequestForOpenedTab(trainUnitInstanceId);
        }

        // サブインベントリの開閉で忘れ、次に開いたとき取り直す
        // Forget on sub-inventory open/close so the next open fetches again
        public void Close()
        {
            CancelInFlightFetch();
            _timetableUpdatedSubscription?.Dispose();
            _timetableUpdatedSubscription = null;
            _isTimetableTabOpen = false;
            _requestedTrainUnitInstanceId = null;
            _unavailableTrainUnitInstanceId = null;
            _requestGeneration++;
            _retriedForRequestedTrain = false;
            _isFetchInFlight = false;
        }

        // 手元に正しい時刻表が届いたら取得不可・取得中のラッチを下ろす
        // A delivered timetable releases the unavailable and in-flight latches so held data is never withheld
        private void OnTimetableDelivered(TrainUnitInstanceId trainUnitInstanceId)
        {
            // 削除でも同じ通知が飛ぶため、実体がある時だけ解除する
            // Removal raises the same notification, so only an existing timetable releases the latches
            if (!_timetableLookup.TryGet(trainUnitInstanceId, out _)) return;
            if (_unavailableTrainUnitInstanceId == trainUnitInstanceId)
            {
                Debug.Log($"[TrainTimetableFetcher] timetable delivered; releasing unavailable latch: {trainUnitInstanceId}");
                _unavailableTrainUnitInstanceId = null;
            }
            if (!_isFetchInFlight || _requestedTrainUnitInstanceId != trainUnitInstanceId) return;
            Debug.Log($"[TrainTimetableFetcher] timetable delivered while fetching; releasing in-flight latch: {trainUnitInstanceId}");
            _isFetchInFlight = false;
        }

        public bool IsUnavailable(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _unavailableTrainUnitInstanceId == trainUnitInstanceId;
        }

        // 取得中は古いキャッシュより読み込み中を優先させる口
        // Lets the DTO prefer a loading view over a stale cache while a fetch is in flight
        public bool IsFetchInFlight(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _isFetchInFlight && _requestedTrainUnitInstanceId == trainUnitInstanceId;
        }

        // 走っている取得を打ち切って新しいトークンを配る
        // Cancel the running fetch and hand out a fresh token
        private CancellationToken RestartCancellation()
        {
            CancelInFlightFetch();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        // 放棄した取得は無音にせずログへ残す
        // An abandoned fetch is logged rather than dropped silently
        private void CancelInFlightFetch()
        {
            if (_isFetchInFlight) Debug.Log($"[TrainTimetableFetcher] cancelling in-flight timetable request: {_requestedTrainUnitInstanceId}");
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTaskVoid FetchTimetableAsync(TrainUnitInstanceId trainUnitInstanceId, int generation, CancellationToken ct)
        {
            Debug.Log($"[TrainTimetableFetcher] requesting timetable: {trainUnitInstanceId}");
            var response = await ClientContext.VanillaApi.Response.Train.GetTrainTimetable(trainUnitInstanceId, ct);
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
            // 古い世代の成功応答は捨てる
            // Like the failure paths, a success from an old generation is discarded
            if (generation != _requestGeneration)
            {
                Debug.LogWarning($"[TrainTimetableFetcher] discarding stale timetable response: {trainUnitInstanceId} (generation {generation})");
                return;
            }
            _isFetchInFlight = false;
            _timetables.Apply(response.Timetable.ToModel());

            #region Internal

            void OnNoResponse()
            {
                // 応答なしは「列車が無い」と区別してログする
                // Log no-response separately from "no train"; only the latest request decides what follows
                Debug.LogWarning($"[TrainTimetableFetcher] no response for timetable request: {trainUnitInstanceId}");
                if (generation != _requestGeneration) return;
                // 再試行は要求する列車ごとに1回まで
                // Retry at most once per requested train so a deterministic failure does not keep requesting
                if (_retriedForRequestedTrain)
                {
                    Debug.LogWarning($"[TrainTimetableFetcher] retry already used; unavailable until reopen: {trainUnitInstanceId}");
                    MarkUnavailable();
                    return;
                }
                _retriedForRequestedTrain = true;
                FetchTimetableAsync(trainUnitInstanceId, ++_requestGeneration, ct).Forget();
            }

            void MarkUnavailable()
            {
                if (generation != _requestGeneration) return;
                _isFetchInFlight = false;
                _unavailableTrainUnitInstanceId = trainUnitInstanceId;
                _onFetchFailed.OnNext(Unit.Default);
            }

            #endregion
        }
    }
}

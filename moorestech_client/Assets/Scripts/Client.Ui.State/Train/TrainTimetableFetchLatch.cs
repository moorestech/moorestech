using Game.Train.Unit;

namespace Client.Ui.State.Train
{
    // Fetcher の取得ラッチ。要求中の列車・取得中・取得不可・再試行済みを持つ
    // The fetcher's fetch latch: requested train, in-flight, unavailable, and retried flags
    internal sealed class TrainTimetableFetchLatch
    {
        private TrainUnitInstanceId? _unavailableTrainUnitInstanceId;
        private bool _isFetchInFlight;

        public TrainUnitInstanceId? RequestedTrainUnitInstanceId { get; private set; }
        public bool RetriedForRequestedTrain { get; private set; }
        public bool HasFetchInFlight => _isFetchInFlight;

        // 同じ列車の重複要求を畳む判定
        // Folds a duplicate request for the same train
        public bool IsSameRequest(TrainUnitInstanceId trainUnitInstanceId)
        {
            return RequestedTrainUnitInstanceId == trainUnitInstanceId;
        }

        // 再試行の残りは列車ごとに持つので、要求の開始で全部立て直す
        // The retry allowance is per train, so starting a request resets everything
        public void StartRequest(TrainUnitInstanceId trainUnitInstanceId)
        {
            RequestedTrainUnitInstanceId = trainUnitInstanceId;
            _unavailableTrainUnitInstanceId = null;
            RetriedForRequestedTrain = false;
            _isFetchInFlight = true;
        }

        public void MarkRetried()
        {
            RetriedForRequestedTrain = true;
        }

        // 届いた列車が要求中なら取得中を倒す
        // Lowers in-flight when the delivered train is the requested one
        public void ReleaseInFlight(TrainUnitInstanceId deliveredTrainUnitInstanceId)
        {
            if (!_isFetchInFlight || RequestedTrainUnitInstanceId != deliveredTrainUnitInstanceId) return; // silent-ok: 別列車の通知では取得状態を変えない / 対象タブは対応する応答を表示する
            _isFetchInFlight = false;
        }

        public void ReleaseUnavailable(TrainUnitInstanceId deliveredTrainUnitInstanceId)
        {
            if (_unavailableTrainUnitInstanceId != deliveredTrainUnitInstanceId) return; // silent-ok: 別列車の通知では取得不可を解かない / 対象タブは取得不可を表示する
            _unavailableTrainUnitInstanceId = null;
        }

        // 取得中を倒し、取得不可を立てる
        // Lowers in-flight and raises unavailable
        public void MarkUnavailable(TrainUnitInstanceId trainUnitInstanceId)
        {
            _isFetchInFlight = false;
            _unavailableTrainUnitInstanceId = trainUnitInstanceId;
        }

        // 成功応答が届いたので取得中を倒す
        // A success response arrived, so in-flight is lowered
        public void CompleteFetch()
        {
            _isFetchInFlight = false;
        }

        public void Clear()
        {
            RequestedTrainUnitInstanceId = null;
            _unavailableTrainUnitInstanceId = null;
            RetriedForRequestedTrain = false;
            _isFetchInFlight = false;
        }

        public bool IsUnavailable(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _unavailableTrainUnitInstanceId == trainUnitInstanceId;
        }

        public bool IsFetchInFlight(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _isFetchInFlight && RequestedTrainUnitInstanceId == trainUnitInstanceId;
        }
    }
}

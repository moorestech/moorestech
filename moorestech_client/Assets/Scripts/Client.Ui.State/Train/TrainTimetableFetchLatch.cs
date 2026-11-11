using Game.Train.Unit;

namespace Client.Ui.State.Train
{
    // 取得ラッチ(要求列車・取得中/不可・再試行済)
    // Fetch latch: requested train, in-flight, unavailable, retried
    internal sealed class TrainTimetableFetchLatch
    {
        private TrainUnitInstanceId? _unavailableTrainUnitInstanceId;

        public TrainUnitInstanceId? RequestedTrainUnitInstanceId { get; private set; }
        public bool RetriedForRequestedTrain { get; private set; }
        public bool HasFetchInFlight { get; private set; }

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
            HasFetchInFlight = true;
        }

        // 再試行未使用なら印を付けtrue、使用済みはfalse
        // Marks retry used and returns true; false if used
        public bool TryUseRetry()
        {
            if (RetriedForRequestedTrain) return false;
            RetriedForRequestedTrain = true;
            return true;
        }

        // 届いた列車が要求中で取得中なら倒して true
        // Lowers in-flight and returns true when the delivered train is the requested one and in flight
        public bool TryReleaseInFlight(TrainUnitInstanceId deliveredTrainUnitInstanceId)
        {
            if (!HasFetchInFlight || RequestedTrainUnitInstanceId != deliveredTrainUnitInstanceId) return false;
            HasFetchInFlight = false;
            return true;
        }

        // 届いた列車が取得不可なら解いて true
        // Releases unavailable and returns true when the delivered train was unavailable
        public bool TryReleaseUnavailable(TrainUnitInstanceId deliveredTrainUnitInstanceId)
        {
            if (_unavailableTrainUnitInstanceId != deliveredTrainUnitInstanceId) return false;
            _unavailableTrainUnitInstanceId = null;
            return true;
        }

        // 取得中を倒し、取得不可を立てる
        // Lowers in-flight and raises unavailable
        public void MarkUnavailable(TrainUnitInstanceId trainUnitInstanceId)
        {
            HasFetchInFlight = false;
            _unavailableTrainUnitInstanceId = trainUnitInstanceId;
        }

        // 成功応答が届いたので取得中を倒す
        // A success response arrived, so in-flight is lowered
        public void CompleteFetch()
        {
            HasFetchInFlight = false;
        }

        public void Clear()
        {
            RequestedTrainUnitInstanceId = null;
            _unavailableTrainUnitInstanceId = null;
            RetriedForRequestedTrain = false;
            HasFetchInFlight = false;
        }

        public bool IsUnavailable(TrainUnitInstanceId trainUnitInstanceId)
        {
            return _unavailableTrainUnitInstanceId == trainUnitInstanceId;
        }

        public bool IsFetchInFlight(TrainUnitInstanceId trainUnitInstanceId)
        {
            return HasFetchInFlight && RequestedTrainUnitInstanceId == trainUnitInstanceId;
        }
    }
}

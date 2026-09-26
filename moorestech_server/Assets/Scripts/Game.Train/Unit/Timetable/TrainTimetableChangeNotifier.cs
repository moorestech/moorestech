using Game.Train.Event;
using UnityEngine;

namespace Game.Train.Unit
{
    // 時刻表・自動運転の変化通知を1操作につき1回へまとめる
    // Coalesce timetable and auto-run change notifications into one per operation
    public sealed class TrainTimetableChangeNotifier
    {
        private readonly ITrainTimetableNotifyEvent _notifyEvent;
        private readonly TrainUnit _trainUnit;
        private int _batchDepth;
        private bool _pendingChanged;

        public TrainTimetableChangeNotifier(ITrainTimetableNotifyEvent notifyEvent, TrainUnit trainUnit)
        {
            _notifyEvent = notifyEvent;
            _trainUnit = trainUnit;
        }

        // 変化の成立点で呼ぶ。まとめ中は保留し、解除時に1回だけ送る
        // Call where a change is established; while batching it is held and sent once at the end
        public void NotifyChanged()
        {
            if (0 < _batchDepth)
            {
                _pendingChanged = true;
                return;
            }
            _notifyEvent.NotifyTimetableChanged(_trainUnit);
        }

        public void BeginBatch()
        {
            _batchDepth++;
        }

        // changedは操作の前後で実状態が変わったか。入れ子のchangedは外側の判定へ委ねる
        // changed means the operation altered the state; a nested verdict defers to the outer batch
        public void EndBatch(bool changed)
        {
            if (_batchDepth == 0)
            {
                // BeginBatchと対になっていない。深さを負にすると以後の抑止が壊れるので0へ留める
                // Unpaired with BeginBatch; a negative depth would break later suppression, so it is clamped
                Debug.LogError($"[TrainTimetable] EndBatch without BeginBatch: train={_trainUnit.TrainUnitInstanceId}");
                _pendingChanged = false;
                if (changed) _notifyEvent.NotifyTimetableChanged(_trainUnit);
                return;
            }
            _batchDepth--;
            if (0 < _batchDepth) return;
            // 保留中の時刻表変化は実状態が戻っていても届ける。内容が変わったのは事実のため
            // A held timetable change is still delivered even if the flag returned, because the content did change
            var shouldNotify = _pendingChanged || changed;
            _pendingChanged = false;
            if (shouldNotify) _notifyEvent.NotifyTimetableChanged(_trainUnit);
        }
    }
}

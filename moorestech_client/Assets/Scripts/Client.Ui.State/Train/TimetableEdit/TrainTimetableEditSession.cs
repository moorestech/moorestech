using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の即時編集（Web useImmediateTimetable の写し）
    /// Immediate timetable editing (copy of Web useImmediateTimetable)
    /// </summary>
    public sealed class TrainTimetableEditSession
    {
        internal const string ReplaceCommandName = "train_timetable.replace";
        private static readonly TimeSpan PublicationTimeout = TimeSpan.FromMilliseconds(5000);

        private readonly ITrainTimetableCommands _commands;
        private readonly ITrainTimetableWaitTimer _timer;
        private readonly UiCommandResultToaster _toaster;
        private readonly Subject<Unit> _onDisplayChanged = new();
        private EditPhase _phase = IdlePhase.Instance;
        private bool _closed;

        // サーバーの最新の時刻表（列車の識別・並び・駅一覧）。セッションと寿命が同じなので一緒に持つ
        // The server's latest timetable (train, order, stations); it shares the session's lifetime so the session holds it
        public TrainTimetableReadyData Server { get; private set; }
        public IReadOnlyList<TimetableEditStop> DisplayedStops { get; private set; }
        public bool IsPending { get; private set; }
        public IObservable<Unit> OnDisplayChanged => _onDisplayChanged;

        public TrainTimetableEditSession(ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster, TrainTimetableReadyData server)
        {
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
            Server = server;
            DisplayedStops = server.Stops;
        }

        public void ReceiveServer(TrainTimetableReadyData server)
        {
            // 時刻表の中身が替わった公開だけが保留を解く。駅一覧・駅名・貨車だけの組み直しは Server を差し替えるだけで、新しい時刻表の到着とは数えない
            // Only a publication that changed the timetable releases the hold; a rebuild from stations, names or cars only swaps Server and does not count as the new timetable arriving
            var changedTimetable = !HasSameTimetable(Server, server);
            Server = server;
            if (changedTimetable) ReleaseForPublication();
            Publish();

            #region Internal

            void ReleaseForPublication()
            {
                // 受理の後なら解き、送信中なら受理の時点で解くために記録する
                // After acceptance release now; while sending, record it so acceptance releases
                if (_phase is AwaitingPublicationPhase) BackToIdle();
                else if (_phase is SendingPhase sending) sending.MarkPublicationReceived();
            }

            #endregion
        }

        public void Replace(IReadOnlyList<TimetableEditStop> nextStops)
        {
            // 前の編集が終わるまでは古い並びから組んだ編集を受けない
            // Until the previous edit settles, refuse edits built from a stale order
            if (_phase is not IdlePhase)
            {
                Debug.LogWarning("[TrainTimetableEdit] ignored: previous edit is still pending");
                return;
            }
            // silent-ok: 並びが変わらない編集は送る意味が無い / 表示も変わらないので操作した人が見て分かる（Web と同じ）
            if (TimetableEditLogic.SameStopOrder(nextStops, Server.Stops)) return;

            var sending = new SendingPhase(nextStops);
            _phase = sending;
            Publish();
            SendAsync(sending).Forget();

            #region Internal

            async UniTaskVoid SendAsync(SendingPhase sending)
            {
                var stops = sending.Stops;
                var result = await _commands.Replace(TimetableEditLogic.ToReplacePayload(stops));

                // 拒否のトーストは閉じた後でも出す（Web useImmediateTimetable と同じ）。表示は別の列車を動かさないよう捨てる
                // A refusal toasts even after close (like Web useImmediateTimetable); the display update is discarded so it cannot touch another train
                if (result is UiCommandRejected) _toaster.Report(ReplaceCommandName, result);
                if (_closed)
                {
                    var summary = result is UiCommandRejected rejected ? $"rejected {rejected.ToWireCode()}" : "accepted";
                    Debug.LogWarning($"[TrainTimetableEdit] result after close discarded: {summary}");
                    return;
                }

                // 拒否か、送信中に時刻表の公開が先に届いていれば解く。まだなら時間切れを仕掛けて公開を待つ
                // Release on refusal or when a timetable publication arrived while sending; otherwise arm the timeout and wait
                if (result is UiCommandRejected || sending.PublicationReceived) BackToIdle();
                else ArmAwaitingPublication(stops);
                Publish();
            }

            void ArmAwaitingPublication(IReadOnlyList<TimetableEditStop> stops)
            {
                // 段階を先に入れてから時間切れを仕掛ける。タイマーが同期で発火しても BackToIdle がこの段階を見て Idle へ戻せる（D-36）
                // Enter the phase before arming the timeout so a synchronously firing timer still finds this phase and returns to Idle (D-36)
                var awaiting = new AwaitingPublicationPhase(stops);
                _phase = awaiting;
                awaiting.Arm(_timer.After(PublicationTimeout).Subscribe(_ => ReleaseMissingPublication()));
            }

            void ReleaseMissingPublication()
            {
                Debug.LogWarning($"[TrainTimetableEdit] publication did not arrive within {PublicationTimeout.TotalMilliseconds}ms; showing the server order");
                BackToIdle();
                Publish();
            }

            #endregion
        }

        public void Close()
        {
            _closed = true;
            BackToIdle();
        }

        private static bool HasSameTimetable(TrainTimetableReadyData previous, TrainTimetableReadyData next)
        {
            // 走行位置（CurrentIndex）は編集の結果ではないので比べない。発車しただけの公開で保留が解けると古い並びへ戻るため
            // CurrentIndex is not an edit result, so leave it out; a departure-only publication must not release the hold and revert to the old order
            return previous.IsAutoRun == next.IsAutoRun && TimetableEditLogic.SameStopOrder(previous.Stops, next.Stops);
        }

        private void BackToIdle()
        {
            if (_phase is AwaitingPublicationPhase awaiting) awaiting.Disarm();
            _phase = IdlePhase.Instance;
        }

        private void Publish()
        {
            DisplayedStops = _phase switch
            {
                SendingPhase sending => sending.Stops,
                AwaitingPublicationPhase awaiting => awaiting.Stops,
                _ => Server.Stops,
            };
            IsPending = _phase is not IdlePhase;
            _onDisplayChanged.OnNext(Unit.Default);
        }
    }
}

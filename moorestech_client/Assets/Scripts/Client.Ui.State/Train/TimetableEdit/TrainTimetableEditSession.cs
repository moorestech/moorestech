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
    /// 時刻表の即時編集。編集を即送り、受理と同じ並びの公開が揃うまで表示を保留する（Web useImmediateTimetable の写し）
    /// Immediate timetable editing: sends each edit at once and holds the display until acceptance and a matching publication (copy of Web useImmediateTimetable)
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
            // 受理済み保留は同並びの公開で解く
            // Accepted pending releases on matching publication
            Server = server;
            if (_phase is AwaitingPublicationPhase awaiting && TimetableEditLogic.SameStopOrder(awaiting.Stops, server.Stops)) BackToIdle();
            Publish();
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

            _phase = new SendingPhase(nextStops);
            Publish();
            SendAsync(nextStops).Forget();
        }

        public void Close()
        {
            _closed = true;
            BackToIdle();
        }

        private async UniTaskVoid SendAsync(IReadOnlyList<TimetableEditStop> stops)
        {
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

            // 拒否か、公開が先に届いていれば解く。まだなら時間切れを仕掛けて公開を待つ
            // Release on refusal or when the publication already arrived; otherwise arm the timeout and wait
            if (result is UiCommandRejected || TimetableEditLogic.SameStopOrder(stops, Server.Stops)) BackToIdle();
            else ArmAwaitingPublication(stops);
            Publish();
        }

        private void ArmAwaitingPublication(IReadOnlyList<TimetableEditStop> stops)
        {
            // 段階を先に入れてから時間切れを仕掛ける。タイマーが同期で発火しても BackToIdle がこの段階を見て Idle へ戻せる（D-36）
            // Enter the phase before arming the timeout so a synchronously firing timer still finds this phase and returns to Idle (D-36)
            var awaiting = new AwaitingPublicationPhase(stops);
            _phase = awaiting;
            awaiting.Arm(_timer.After(PublicationTimeout).Subscribe(_ => ReleaseMissingPublication()));
        }

        private void ReleaseMissingPublication()
        {
            Debug.LogWarning($"[TrainTimetableEdit] publication did not arrive within {PublicationTimeout.TotalMilliseconds}ms; showing the server order");
            BackToIdle();
            Publish();
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

        // 編集の段階: 待ちなし・送信中・受理後の公開待ち
        // Edit phases: idle, sending, and awaiting publication after acceptance
        private abstract class EditPhase { }

        private sealed class IdlePhase : EditPhase
        {
            public static readonly IdlePhase Instance = new();
        }

        private sealed class SendingPhase : EditPhase
        {
            public IReadOnlyList<TimetableEditStop> Stops { get; }

            public SendingPhase(IReadOnlyList<TimetableEditStop> stops)
            {
                Stops = stops;
            }
        }

        private sealed class AwaitingPublicationPhase : EditPhase
        {
            public IReadOnlyList<TimetableEditStop> Stops { get; }
            private IDisposable _timeout = Disposable.Empty;

            public AwaitingPublicationPhase(IReadOnlyList<TimetableEditStop> stops)
            {
                Stops = stops;
            }

            // 段階に入った後で時間切れの購読を受け取る（同期発火で既に Idle なら、受け取る購読は発火済みで捨ててよい）
            // Receives the timeout subscription after the phase is entered (if a synchronous fire already went Idle, the finished subscription is harmless)
            public void Arm(IDisposable timeout)
            {
                _timeout = timeout;
            }

            public void Disarm()
            {
                _timeout.Dispose();
            }
        }
    }
}

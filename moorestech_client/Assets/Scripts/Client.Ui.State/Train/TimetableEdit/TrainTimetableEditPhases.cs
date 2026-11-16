using System;
using System.Collections.Generic;
using Client.Ui.State.Train.Logic;
using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    // 編集の段階: 待ちなし・送信中・受理後の公開待ち
    // Edit phases: idle, sending, and awaiting publication after acceptance
    internal abstract class EditPhase { }

    internal sealed class IdlePhase : EditPhase
    {
        public static readonly IdlePhase Instance = new();
    }

    internal sealed class SendingPhase : EditPhase
    {
        public IReadOnlyList<TimetableEditStop> Stops { get; }
        public bool PublicationReceived { get; private set; }

        public SendingPhase(IReadOnlyList<TimetableEditStop> stops)
        {
            Stops = stops;
        }

        public void MarkPublicationReceived()
        {
            PublicationReceived = true;
        }
    }

    internal sealed class AwaitingPublicationPhase : EditPhase
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

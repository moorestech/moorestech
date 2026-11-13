using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit.Send;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 送られた置換・自動運転を記録し、結果はテストが手で返す
    /// Records sent replaces and auto-run toggles; tests resolve the results by hand
    /// </summary>
    public sealed class FakeTrainTimetableCommands : ITrainTimetableCommands
    {
        public int OpenRequestCount { get; private set; }
        public List<IReadOnlyList<TimetableReplaceStop>> SentReplaces { get; } = new();
        public List<bool> SentAutoRuns { get; } = new();
        private readonly Queue<UniTaskCompletionSource<UiCommandResult>> _pending = new();

        public void RequestOpen()
        {
            OpenRequestCount++;
        }

        public UniTask<UiCommandResult> Replace(IReadOnlyList<TimetableReplaceStop> stops)
        {
            SentReplaces.Add(stops);
            return Enqueue();
        }

        public UniTask<UiCommandResult> SetAutoRun(bool enabled)
        {
            SentAutoRuns.Add(enabled);
            return Enqueue();
        }

        public void Accept()
        {
            _pending.Dequeue().TrySetResult(UiCommandAccepted.Instance);
        }

        // 理由ごとの拒否。診断へ写す（D-31・A-260）
        // Per-reason refusal; FailureReason to diagnostic (D-31, A-260)
        public void Reject(UiCommandRejectReason reason, string serverFailureReason)
        {
            _pending.Dequeue().TrySetResult(UiCommandRejected.WithDiagnostic(reason, serverFailureReason));
        }

        private UniTask<UiCommandResult> Enqueue()
        {
            var source = new UniTaskCompletionSource<UiCommandResult>();
            _pending.Enqueue(source);
            return source.Task;
        }
    }
}

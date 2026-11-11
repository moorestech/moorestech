using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 駅名・モードの送信を記録し、結果はテストが手で返す
    /// Records station name and mode sends; tests resolve the results by hand
    /// </summary>
    public sealed class FakeTrainBlockCommands : ITrainBlockCommands
    {
        public List<string> SentNames { get; } = new();
        public List<TrainPlatformTransferMode> SentModes { get; } = new();
        private readonly Queue<UniTaskCompletionSource<UiCommandResult>> _pending = new();

        public UniTask<UiCommandResult> SetStationName(string name)
        {
            SentNames.Add(name);
            return Enqueue();
        }

        public UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode)
        {
            SentModes.Add(mode);
            return Enqueue();
        }

        public void Accept()
        {
            _pending.Dequeue().TrySetResult(UiCommandAccepted.Instance);
        }

        // 理由ごとの拒否。Detail はサーバーの FailureReason の写し（無ければ空文字。D-31）
        // Per-reason refusal; Detail mirrors the server FailureReason (empty when absent; D-31)
        public void Reject(UiCommandRejectReason reason, string serverFailureReason)
        {
            _pending.Dequeue().TrySetResult(new UiCommandRejected(reason, serverFailureReason));
        }

        private UniTask<UiCommandResult> Enqueue()
        {
            var source = new UniTaskCompletionSource<UiCommandResult>();
            _pending.Enqueue(source);
            return source.Task;
        }
    }
}

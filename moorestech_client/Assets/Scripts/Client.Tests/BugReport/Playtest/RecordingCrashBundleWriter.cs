using System.Collections.Generic;
using Client.Game.InGame.BugReport.LastSession;
using Cysharp.Threading.Tasks;

namespace Client.Tests.BugReport
{
    // テスト用の記録ダブル。ゲートが「何をどんな説明文で書かせたか」を、本番コードに検証用の口を残さず観測する
    // A recording double for tests; it observes what the gate asked to write, with what description, without leaving a test-only port in production code
    public sealed class RecordingCrashBundleWriter : ICrashBundleWriter
    {
        // 書けたことにする戻り値。nullにすると書き出し失敗（WriteFailed）を再現できる
        // The directory the write pretends to produce; null reproduces a failed write (WriteFailed)
        private readonly string _writtenDirectory;

        public List<string> Descriptions { get; } = new();

        public RecordingCrashBundleWriter(string writtenDirectory)
        {
            _writtenDirectory = writtenDirectory;
        }

        public UniTask<string> WriteAsync(PreviousSessionArtifacts artifacts, string description)
        {
            Descriptions.Add(description);
            return UniTask.FromResult(_writtenDirectory);
        }
    }
}

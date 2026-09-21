using System;
using System.Threading;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;

namespace Client.Tests.WebUi.Boot
{
    // 実ホストのバインド解除をテストから解放するまで保留する
    // Hold the real host's binding cleanup until the test releases it
    internal sealed class BlockingShutdownTopic : ITopicHandler, IDisposable
    {
        private readonly ManualResetEventSlim _disposalStarted = new();
        private readonly ManualResetEventSlim _release = new();
        private int _disposalCompleted;

        internal bool DisposalCompleted => Volatile.Read(ref _disposalCompleted) != 0;

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult("{}");
        }

        public void Dispose()
        {
            // テスト失敗時にも停止スレッドを永久に残さない
            // Bound the wait so a failing test cannot leave the stop thread blocked forever
            _disposalStarted.Set();
            if (!_release.Wait(TimeSpan.FromSeconds(3)))
                UnityEngine.Debug.LogError("ホスト停止テストの解放通知が届きませんでした");
            Volatile.Write(ref _disposalCompleted, 1);
        }

        internal bool WaitForDisposal()
        {
            return _disposalStarted.Wait(TimeSpan.FromSeconds(2));
        }

        internal void Release()
        {
            _release.Set();
        }
    }
}

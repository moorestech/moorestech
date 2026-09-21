using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Boot;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Host = Client.WebUiHost.Boot.WebUiHost;

namespace Client.Tests.WebUi.Boot
{
    public class WebUiHostStopTest
    {
        [Test]
        public async Task 重複停止後の再起動も最初の停止完了を待つ()
        {
            var hubField = typeof(Host).GetField("_hub", BindingFlags.Static | BindingFlags.NonPublic);
            var kestrelField = typeof(Host).GetField("_kestrel", BindingFlags.Static | BindingFlags.NonPublic);
            var stopTaskField = typeof(Host).GetField("_stopTask", BindingFlags.Static | BindingFlags.NonPublic);
            var topic = new BlockingShutdownTopic();
            var hub = new WebSocketHub();
            hub.RegisterTopic("restart-test", topic);
            hubField.SetValue(null, hub);
            Host.Stop();
            var firstStopTask = (Task)stopTaskField.GetValue(null);

            try
            {
                Assert.IsTrue(topic.WaitForDisposal());
                Host.Stop();
                Assert.AreSame(firstStopTask, stopTaskField.GetValue(null));

                // 前回停止待ちの直後に既存ホスト判定で戻し、外部プロセスは起動しない
                // Return through the existing-host branch after the stop wait without starting external processes
                kestrelField.SetValue(null, new KestrelServer());
                var restart = Host.StartAsync(CancellationToken.None).AsTask();
                Assert.IsFalse(restart.IsCompleted, "再起動が未完了の最初の停止を待たずに進んだ");

                topic.Release();
                Assert.IsTrue(await restart);
                Assert.IsTrue(firstStopTask.IsCompleted);
            }
            finally
            {
                // assertion失敗でも保留中の停止とフィールドを次テストへ持ち越さない
                // Release the pending stop and fields even when an assertion fails
                topic.Release();
                await firstStopTask;
                kestrelField.SetValue(null, null);
                hubField.SetValue(null, null);
            }
        }
    }
}

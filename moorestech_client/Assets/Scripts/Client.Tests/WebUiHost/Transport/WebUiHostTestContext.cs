using System;
using System.Reflection;
using System.Threading.Tasks;
using Client.WebUiHost.Boot.Transport;
using NUnit.Framework;
using Host = Client.WebUiHost.Boot.WebUiHost;

namespace Client.Tests.WebUiHost.Transport
{
    // 各fixtureの模擬起動世代を隔離する
    // Isolate each fixture's simulated host lifetime
    internal sealed class WebUiHostTestContext : IDisposable
    {
        private readonly FieldInfo _stopTask = typeof(Host).GetField("_stopTask", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly object _previous;

        public WebUiHostTestContext()
        {
            Assert.IsNull(Host.Hub, "An actual host must not be running during this fixture");
            _previous = _stopTask.GetValue(null);
            _stopTask.SetValue(null, null);
        }

        public void Dispose()
        {
            _stopTask.SetValue(null, _previous);
        }

        // 停止はスレッドプールでログを出すため、LogScope の毎フレーム列挙と競合しないようフレームを譲らず停止タスクを待つ
        // Stop logs on the thread pool, so wait on the stop task without yielding to avoid racing LogScope's per-frame enumeration
        public static WebUiStopResult StopWithoutYield()
        {
            Host.StopAsync();
            var stopping = (Task<WebUiStopResult>)typeof(Host).GetField("_stopTask", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Assert.IsTrue(stopping.Wait(TimeSpan.FromSeconds(4)), "WebUiHost stop must complete within the editor stop budget");
            return stopping.Result;
        }
    }
}

using System;
using System.Reflection;
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
    }
}

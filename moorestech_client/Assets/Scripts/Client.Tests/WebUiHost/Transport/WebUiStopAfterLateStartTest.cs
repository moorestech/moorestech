using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Client.WebUiHost.Boot.Transport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using Host = Client.WebUiHost.Boot.WebUiHost;

namespace Client.Tests.WebUiHost.Transport
{
    // 停止結果を持った後に起動が完了しても、次の停止がその起動分を止めることを固定する
    // Pin that a stop issued after a late-finishing start still stops what that start opened
    public class WebUiStopAfterLateStartTest
    {
        private WebUiHostTestContext _lifetime;
        private readonly FieldInfo _kestrelField = typeof(Host).GetField("_kestrel", BindingFlags.NonPublic | BindingFlags.Static);
        [SetUp] public void BeginLifetime() => _lifetime = new WebUiHostTestContext();

        [TearDown]
        public void EndLifetime()
        {
            _kestrelField.SetValue(null, null);
            _lifetime.Dispose();
        }

        [Test]
        public void 停止結果の保持後に起動が埋めたKestrelも次の停止で止める()
        {
            // 起動途中の停止は、まだ空のフィールドに対して停止結果を作る
            // A stop during startup builds its result against still-empty fields
            var early = WebUiHostTestContext.StopWithoutYield();
            Assert.IsTrue(early.Succeeded);

            // その後に起動が完了してフィールドが埋まる
            // Startup then finishes and fills the fields
            var webHost = new RecordingWebHost();
            var kestrel = new KestrelServer();
            typeof(KestrelServer).GetField("_webHost", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(kestrel, webHost);
            _kestrelField.SetValue(null, kestrel);

            WebUiHostTestContext.StopWithoutYield();

            Assert.AreEqual(1, webHost.StopAttempts, "保持済みの停止結果が返り、起動後の Kestrel が止まらなかった");
            Assert.IsNull(_kestrelField.GetValue(null), "停止後も Kestrel がフィールドに残っている");
        }

        private sealed class RecordingWebHost : IWebHost
        {
            public int StopAttempts { get; private set; }
            public IFeatureCollection ServerFeatures => throw new NotSupportedException();
            public IServiceProvider Services => throw new NotSupportedException();
            public void Dispose() { }
            public void Start() => throw new NotSupportedException();
            public Task StartAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task StopAsync(CancellationToken cancellationToken)
            {
                StopAttempts++;
                return Task.CompletedTask;
            }
        }
    }
}

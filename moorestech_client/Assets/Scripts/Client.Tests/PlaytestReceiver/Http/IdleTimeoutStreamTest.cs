using System;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class IdleTimeoutStreamTest
    {
        [Test]
        public void 読み出しが進む間は期限が延び進まなければ切れる()
        {
            // 非前面のUnity Editorは実時間が遅くなりうるため、期限と待ち時間に余裕を持たせる
            // A backgrounded Unity Editor can run real time slower, so the deadline and waits carry slack
            using var idle = new CancellationTokenSource();
            using var stream = new IdleTimeoutStream(new MemoryStream(new byte[64]), idle, TimeSpan.FromMilliseconds(600));
            var buffer = new byte[16];

            Thread.Sleep(400);
            Assert.AreEqual(16, stream.Read(buffer, 0, 16));
            Thread.Sleep(400);
            Assert.IsFalse(idle.IsCancellationRequested, "進んでいる間に切れてはいけない");

            Thread.Sleep(1000);
            Assert.IsTrue(idle.IsCancellationRequested, "進まなくなったら切れる");
        }
    }
}

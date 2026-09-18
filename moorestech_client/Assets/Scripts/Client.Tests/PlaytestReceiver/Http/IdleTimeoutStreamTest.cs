using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class IdleTimeoutStreamTest
    {
        // 非前面のUnity Editorは実時間が遅くなりうるため期限は長めにする。read前後の各待ちは期限より短く、合計は期限の1.5倍にして
        // 延長処理が無ければ必ず期限切れになるようにする（待ちが期限の1/4だと延長の有無を区別できない）
        // A backgrounded Unity Editor can run real time slower, so the deadline is generous. Each wait around
        // the read stays under the deadline while their sum exceeds it 1.5x, so a missing extension always trips
        private static readonly TimeSpan Deadline = TimeSpan.FromMilliseconds(2000);
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(1500);

        // mutationでReadAsync側の延長処理を消してもテストが緑になっていた。両経路を切り替えて検査する
        // A mutation removing ReadAsync's extension still passed; this switches between both read paths
        [TestCase(false)]
        [TestCase(true)]
        public void 読み出しが進む間は期限が延び進まなければ切れる(bool useAsync)
        {
            using var idle = new CancellationTokenSource();
            using var stream = new IdleTimeoutStream(new MemoryStream(new byte[64]), idle, Deadline);
            var buffer = new byte[16];

            Thread.Sleep(Step);
            var read = useAsync ? stream.ReadAsync(buffer, 0, 16, CancellationToken.None).GetAwaiter().GetResult() : stream.Read(buffer, 0, 16);
            Assert.AreEqual(16, read);
            Thread.Sleep(Step);
            Assert.IsFalse(idle.IsCancellationRequested, "進んでいる間に切れてはいけない");

            Thread.Sleep(Deadline + Deadline);
            Assert.IsTrue(idle.IsCancellationRequested, "進まなくなったら切れる");
        }

        // 本番はStreamContent経由でHttpClientに送出される。その経路でも期限が延びることを確かめる
        // コピー前後にそれぞれ期限の3/4待つことで、延長が無ければ合計が期限を超えて切れるようにする
        // Production sends through StreamContent into HttpClient; this confirms the deadline extends on that path too.
        // Waiting 3/4 of the deadline on each side of the copy means a missing extension always exceeds it
        [Test]
        public void StreamContent経由のCopyToAsyncでも期限が延びる()
        {
            using var idle = new CancellationTokenSource();
            using var stream = new IdleTimeoutStream(new MemoryStream(new byte[64]), idle, Deadline);
            using var content = new StreamContent(stream);

            Thread.Sleep(Step);
            content.CopyToAsync(Stream.Null).GetAwaiter().GetResult();
            Thread.Sleep(Step);
            Assert.IsFalse(idle.IsCancellationRequested, "コピー直後は切れてはいけない");
        }
    }
}

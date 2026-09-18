using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class IdleTimeoutFileContentTest
    {
        // 非前面のUnity Editorは実時間が遅くなりうるため期限は長めにする。1回のwriteは期限より短く、3回の合計は期限を超えるので、
        // 書き出しごとの延長が無ければ必ず切れる。本文後の待ちも期限を超えるので、応答待ちの期限へ切り替わっていなければ切れる
        // A backgrounded Unity Editor can run real time slower, so the deadline is generous. Each write stays under it while three exceed it,
        // so a missing per-write extension always trips; the wait after the body also exceeds it, so a missing switch to the response deadline trips too
        private static readonly TimeSpan IdleDeadline = TimeSpan.FromMilliseconds(2000);
        private static readonly TimeSpan WriteDuration = TimeSpan.FromMilliseconds(1200);
        private static readonly TimeSpan ResponseDeadline = TimeSpan.FromMinutes(10);

        [Test]
        public void 書き出しが進む間は切れず本文後は応答待ちの期限に切り替わる()
        {
            using var deadline = new CancellationTokenSource();
            using var content = new IdleTimeoutFileContent(new MemoryStream(new byte[IdleTimeoutFileContent.ChunkBytes * 3]), IdleTimeoutFileContent.ChunkBytes * 3, deadline, IdleDeadline, ResponseDeadline);
            var output = new SlowWriteStream(WriteDuration);

            var watch = Stopwatch.StartNew();
            Task.Run(() => content.CopyToAsync(output)).GetAwaiter().GetResult();
            Assert.Greater(watch.Elapsed, IdleDeadline, "延長の有無を区別できる長さで書き出していない");
            Assert.AreEqual(3, output.WriteCount);
            Assert.IsFalse(deadline.IsCancellationRequested, "書き出しが進んでいる間に切れてはいけない");

            Thread.Sleep(IdleDeadline + IdleDeadline);
            Assert.IsFalse(deadline.IsCancellationRequested, "本文を送り終えた後はアイドル期限で切らない");
        }

        [Test]
        public void 書き出しが止まればアイドル期限で切れる()
        {
            using var deadline = new CancellationTokenSource();
            using var content = new IdleTimeoutFileContent(new MemoryStream(new byte[16]), 16, deadline, IdleDeadline, ResponseDeadline);

            var copy = Task.Run(() => content.CopyToAsync(new StalledWriteStream()));
            var ended = Task.WhenAny(copy, Task.Delay(TimeSpan.FromSeconds(60))).GetAwaiter().GetResult() == copy;
            Assert.IsTrue(ended, "止まった書き出しが期限で切れなかった");
            Assert.IsTrue(copy.IsCanceled || copy.IsFaulted, "止まった書き出しが成功扱いになった");
            Assert.IsTrue(deadline.IsCancellationRequested);
        }

        [Test]
        public void 本文を送り終えたら応答待ちの期限で切れる()
        {
            using var deadline = new CancellationTokenSource();
            using var content = new IdleTimeoutFileContent(new MemoryStream(new byte[16]), 16, deadline, TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(200));

            Task.Run(() => content.CopyToAsync(Stream.Null)).GetAwaiter().GetResult();
            Thread.Sleep(IdleDeadline);
            Assert.IsTrue(deadline.IsCancellationRequested, "応答待ちの期限へ切り替わっていない");
        }

        [Test]
        public void 宣言より短いファイルは手元の読み取り失敗として止める()
        {
            using var deadline = new CancellationTokenSource();
            using var content = new IdleTimeoutFileContent(new MemoryStream(new byte[8]), 16, deadline, IdleDeadline, ResponseDeadline);

            // HttpContentは本文の例外を包み直しうるので、型ではなく記録された理由で確かめる
            // HttpContent may rewrap the body's exception, so the recorded reason is checked instead of the type
            Assert.Catch(() => Task.Run(() => content.CopyToAsync(Stream.Null)).GetAwaiter().GetResult());
            StringAssert.Contains("declared 16 bytes", content.LocalReadFailure);
        }

        [Test]
        public void 本文を2度書いても2度目も先頭から全部送る()
        {
            using var deadline = new CancellationTokenSource();
            var payload = new byte[IdleTimeoutFileContent.ChunkBytes + 5];
            for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);
            using var content = new IdleTimeoutFileContent(new MemoryStream(payload), payload.Length, deadline, IdleDeadline, ResponseDeadline);

            var first = new MemoryStream();
            var second = new MemoryStream();
            Task.Run(() => content.CopyToAsync(first)).GetAwaiter().GetResult();
            Task.Run(() => content.CopyToAsync(second)).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(payload, first.ToArray());
            CollectionAssert.AreEqual(payload, second.ToArray());
            Assert.IsNull(content.LocalReadFailure);
        }

        // 待ちは Task.Run 上で走らせ、メインスレッドの同期コンテキストへ戻ろうとして固まるのを避ける
        // Waits run under Task.Run so they never deadlock trying to return to the main thread's synchronization context
        // 1回のwriteに一定時間かかる出力。遅いが進む回線の代わり
        // An output where each write takes a fixed time; stands in for a slow but moving line
        private sealed class SlowWriteStream : MemoryStream
        {
            private readonly TimeSpan _writeDuration;
            public int WriteCount;

            public SlowWriteStream(TimeSpan writeDuration) { _writeDuration = writeDuration; }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await Task.Delay(_writeDuration, cancellationToken);
                WriteCount++;
            }
        }

        // 期限が切れるまで書き終わらない出力。完全に止まった回線の代わり
        // An output whose write never finishes until the deadline fires; stands in for a fully stalled line
        private sealed class StalledWriteStream : MemoryStream
        {
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.Delay(Timeout.Infinite, cancellationToken);
            }
        }
    }
}

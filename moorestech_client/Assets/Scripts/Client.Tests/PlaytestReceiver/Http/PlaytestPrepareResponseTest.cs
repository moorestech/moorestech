using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class PlaytestPrepareResponseTest
    {
        private static readonly PlaytestDeclaredFile[] Declared = { new("a/b.bin", 3, "/box/a/b.bin"), new("c.bin", 5, "/box/c.bin") };

        [Test]
        public void prepared応答はuploadsとconflictsを宣言のファイルへ結び付けてパースする()
        {
            var ok = TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a/b.bin\",\"url\":\"https://r2/x\",\"bytes\":3}],\"conflicts\":[{\"path\":\"c.bin\",\"expectedBytes\":5,\"actualBytes\":7}],\"expiresInSeconds\":3600}", out var response, out var detail);
            Assert.IsTrue(ok, detail);
            Assert.AreEqual(PlaytestPrepareOutcome.Prepared, response.Outcome);
            Assert.AreEqual(1, response.Uploads.Count);
            Assert.AreSame(Declared[0], response.Uploads[0].File);
            Assert.AreEqual(1, response.Conflicts.Count);
            Assert.AreSame(Declared[1], response.Conflicts[0].File);
            Assert.AreEqual(7, response.Conflicts[0].ActualBytes);
        }

        [Test]
        public void 宣言のワイヤ表現は世代を載せAbsolutePathは乗らない()
        {
            var body = PlaytestReceiverClient.ComposeDeclarationBody(2, new[] { new PlaytestDeclaredFile("a/b.bin", 3, "C:/secret/a/b.bin") });
            Assert.AreEqual("{\"generation\":2,\"files\":[{\"path\":\"a/b.bin\",\"bytes\":3}]}", body);
            StringAssert.DoesNotContain("secret", body);
        }

        [Test]
        public void ackedの応答は空のuploadsで通る()
        {
            Assert.IsTrue(TryParse("{\"outcome\":\"acked\"}", out var response, out _));
            Assert.AreEqual(PlaytestPrepareOutcome.AlreadyAcked, response.Outcome);
        }

        [Test]
        public void JSONでない_uploadsが無い_項目が欠ける応答は理由付きで失敗する()
        {
            Assert.IsFalse(TryParse("not json", out _, out var d1)); StringAssert.Contains("not JSON", d1);
            Assert.IsFalse(TryParse("{}", out _, out var d2)); StringAssert.Contains("outcome", d2);
            Assert.IsFalse(TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a/b.bin\",\"bytes\":3}],\"conflicts\":[]}", out _, out var d3)); StringAssert.Contains("malformed", d3);
            Assert.IsFalse(TryParse("{\"outcome\":\"prepared\",\"uploads\":[]}", out _, out var d4)); StringAssert.Contains("conflicts", d4);
        }

        // 形の崩れた外部JSONでも例外を出さず、理由付きの false で返す
        // Malformed external JSON never throws; it returns false with a reason
        [TestCase("{\"outcome\":{},\"uploads\":[],\"conflicts\":[]}", "outcome")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[\"a.bin\"],\"conflicts\":[]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[[1]],\"conflicts\":[]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":{},\"url\":\"https://r2/x\",\"bytes\":3}],\"conflicts\":[]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a/b.bin\",\"url\":[\"x\"],\"bytes\":3}],\"conflicts\":[]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[],\"conflicts\":[{\"path\":\"c.bin\",\"expectedBytes\":5}]}", "actualBytes")]
        public void 想定外の型の応答は例外を出さず理由付きで失敗する(string body, string expectedDetail)
        {
            Assert.IsFalse(TryParse(body, out var response, out var detail));
            Assert.IsNull(response);
            StringAssert.Contains(expectedDetail, detail);
        }

        // 相対URLはPUTの送信で例外になり走行ごと落ち、http は署名を平文で流す。長さ違い・重複・宣言外のパスも契約違反
        // A relative URL throws when sent and ends the run, plain http leaks the signature; a wrong length, a duplicate or an undeclared path also breach the contract
        [TestCase("{\"path\":\"a/b.bin\",\"url\":\"/relative/a\",\"bytes\":3}", "absolute https")]
        [TestCase("{\"path\":\"a/b.bin\",\"url\":\"http://r2/x\",\"bytes\":3}", "absolute https")]
        [TestCase("{\"path\":\"a/b.bin\",\"url\":\"https://r2/x\",\"bytes\":4}", "differs from the declared 3")]
        [TestCase("{\"path\":\"a/b.bin\",\"url\":\"https://r2/x\"}", "differs from the declared 3")]
        [TestCase("{\"path\":\"x.bin\",\"url\":\"https://r2/x\",\"bytes\":3}", "undeclared path")]
        [TestCase("{\"path\":\"a/b.bin\",\"url\":\"https://r2/x\",\"bytes\":3},{\"path\":\"a/b.bin\",\"url\":\"https://r2/y\",\"bytes\":3}", "duplicated path")]
        public void 宣言やURLの形と食い違うuploadsは理由付きで失敗する(string entries, string expectedDetail)
        {
            Assert.IsFalse(TryParse($"{{\"outcome\":\"prepared\",\"uploads\":[{entries}],\"conflicts\":[]}}", out _, out var detail));
            StringAssert.Contains(expectedDetail, detail);
        }

        // 失敗の理由は UPLOAD_ATTEMPTS とログへ残るので、署名付きURL（1時間有効な権限）を含めない
        // The failure reason ends up in UPLOAD_ATTEMPTS and logs, so it never carries the presigned URL (an hour-long authority)
        [Test]
        public void 失敗の理由に署名付きURLを載せない()
        {
            Assert.IsFalse(TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a/b.bin\",\"url\":\"https://r2/x?X-Amz-Signature=deadbeef\",\"bytes\":9}],\"conflicts\":[]}", out _, out var detail));
            StringAssert.DoesNotContain("X-Amz-Signature", detail);
        }

        private static bool TryParse(string body, out PlaytestPrepareResponse response, out string detail)
        {
            return PlaytestPrepareResponse.TryParse(body, Declared, out response, out detail);
        }
    }
}

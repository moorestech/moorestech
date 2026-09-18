using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class PlaytestPrepareResponseTest
    {
        [Test]
        public void prepared応答はuploads配列をパースする()
        {
            var ok = PlaytestPrepareResponse.TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a/b.bin\",\"url\":\"https://r2/x\",\"bytes\":3}],\"expiresInSeconds\":3600}", out var response, out var detail);
            Assert.IsTrue(ok, detail);
            Assert.AreEqual(PlaytestPrepareOutcome.Prepared, response.Outcome);
            Assert.AreEqual(1, response.Uploads.Count);
            Assert.AreEqual("a/b.bin", response.Uploads[0].Path);
        }

        [Test]
        public void 宣言のワイヤ表現にAbsolutePathは乗らない()
        {
            var body = PlaytestReceiverClient.ComposeDeclarationBody(new[] { new PlaytestDeclaredFile("a/b.bin", 3, "C:/secret/a/b.bin") });
            Assert.AreEqual("{\"files\":[{\"path\":\"a/b.bin\",\"bytes\":3}]}", body);
            StringAssert.DoesNotContain("secret", body);
        }

        [Test]
        public void ackedの応答は空のuploadsで通る()
        {
            Assert.IsTrue(PlaytestPrepareResponse.TryParse("{\"outcome\":\"acked\"}", out var response, out _));
            Assert.AreEqual(PlaytestPrepareOutcome.AlreadyAcked, response.Outcome);
        }

        [Test]
        public void JSONでない_uploadsが無い_項目が欠ける応答は理由付きで失敗する()
        {
            Assert.IsFalse(PlaytestPrepareResponse.TryParse("not json", out _, out var d1)); StringAssert.Contains("not JSON", d1);
            Assert.IsFalse(PlaytestPrepareResponse.TryParse("{}", out _, out var d2)); StringAssert.Contains("outcome", d2);
            Assert.IsFalse(PlaytestPrepareResponse.TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a\"}]}", out _, out var d3)); StringAssert.Contains("malformed", d3);
        }

        // 形の崩れた外部JSONでも例外を出さず、理由付きの false で返す
        // Malformed external JSON never throws; it returns false with a reason
        [TestCase("{\"outcome\":{},\"uploads\":[]}", "outcome")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[\"a.bin\"]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[[1]]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":{},\"url\":\"https://r2/x\",\"bytes\":3}]}", "malformed")]
        [TestCase("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a\",\"url\":[\"x\"],\"bytes\":3}]}", "malformed")]
        public void 想定外の型の応答は例外を出さず理由付きで失敗する(string body, string expectedDetail)
        {
            Assert.IsFalse(PlaytestPrepareResponse.TryParse(body, out var response, out var detail));
            Assert.IsNull(response);
            StringAssert.Contains(expectedDetail, detail);
        }
    }
}

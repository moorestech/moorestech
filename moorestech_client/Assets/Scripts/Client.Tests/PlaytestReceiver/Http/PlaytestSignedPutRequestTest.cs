using System;
using System.Linq;
using System.Net.Http;
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class PlaytestSignedPutRequestTest
    {
        private const string SignedUrl = "https://acc.r2.cloudflarestorage.com/bucket/reports/7656/b/a.bin?X-Amz-Credential=KEY%2F20260918&X-Amz-Signature=deadbeef";

        // 受け口は If-None-Match: * を署名に含めて発行する。付け忘れると署名不一致で全PUTが403になり、付けていれば既存キーの上書きも防げる
        // The receiver signs If-None-Match: *; leaving it off fails every PUT with a signature mismatch, and sending it forbids overwriting an existing key
        [Test]
        public void 署名付きPUTはIf_None_Match_アスタリスクを付けBearerを付けない()
        {
            using var request = PlaytestReceiverClient.CreateSignedPutRequest(SignedUrl, new ByteArrayContent(new byte[1]));

            Assert.AreEqual(HttpMethod.Put, request.Method);
            Assert.AreEqual("*", request.Headers.IfNoneMatch.Single().Tag);
            Assert.IsNull(request.Headers.Authorization);
        }

        [Test]
        public void ログに出す宛先は署名付きURLのクエリを含まない()
        {
            var logged = PlaytestReceiverClient.DescribeTargetForLog(new Uri(SignedUrl));

            Assert.AreEqual("https://acc.r2.cloudflarestorage.com/bucket/reports/7656/b/a.bin", logged);
            StringAssert.DoesNotContain("X-Amz", logged);
        }
    }
}

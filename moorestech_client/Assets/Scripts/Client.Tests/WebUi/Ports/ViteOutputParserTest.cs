using Client.WebUiHost.Vite;
using NUnit.Framework;

namespace Client.Tests.WebUi.Ports
{
    public class ViteOutputParserTest
    {
        [Test]
        public void プレーンなLocal行からポートを取得できる()
        {
            var ok = ViteOutputParser.TryParseLocalPort("  ➜  Local:   http://127.0.0.1:25173/", out var port);
            Assert.IsTrue(ok);
            Assert.AreEqual(25173, port);
        }

        [Test]
        public void ANSIカラーコード付きのLocal行からポートを取得できる()
        {
            var line = "  ➜  Local:   \x1b[36mhttp://127.0.0.1:\x1b[1m25174\x1b[22m/\x1b[39m";
            var ok = ViteOutputParser.TryParseLocalPort(line, out var port);
            Assert.IsTrue(ok);
            Assert.AreEqual(25174, port);
        }

        [Test]
        public void ポートを含まない行はfalseを返す()
        {
            Assert.IsFalse(ViteOutputParser.TryParseLocalPort("  VITE v5.4.0  ready in 312 ms", out _));
            Assert.IsFalse(ViteOutputParser.TryParseLocalPort("  ➜  Network: use --host to expose", out _));
            Assert.IsFalse(ViteOutputParser.TryParseLocalPort("", out _));
        }
    }
}

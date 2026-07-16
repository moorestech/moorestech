using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using NUnit.Framework;

namespace Client.Tests.WebUi.Ports
{
    public class WebUiAllowedOriginTest
    {
        [Test]
        public void 確定済みViteポートのオリジンのみ許可する()
        {
            WebUiPortConfig.SetVitePort(25174);
            Assert.IsTrue(WebUiEndpoints.IsAllowedOrigin("http://localhost:25174"));
            Assert.IsTrue(WebUiEndpoints.IsAllowedOrigin("http://127.0.0.1:25174"));
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://localhost:5173"));
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://evil.example.com:25174"));
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin(""));
        }

        [Test]
        public void Viteポート未確定時は全拒否する()
        {
            WebUiPortConfig.SetVitePort(0);
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://localhost:25173"));

            // ガード除去時に "http://localhost:0" が素通りしないこと（変異検出用の直接ケース）
            // Ensure "http://localhost:0" cannot slip through if the unresolved guard were removed (mutation-killing case)
            Assert.IsFalse(WebUiEndpoints.IsAllowedOrigin("http://localhost:0"));
        }
    }
}

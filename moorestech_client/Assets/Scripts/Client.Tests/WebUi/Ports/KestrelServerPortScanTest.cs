using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using NUnit.Framework;

namespace Client.Tests.WebUi.Ports
{
    public class KestrelServerPortScanTest
    {
        [Test]
        public void ベースポート占有時は次のポートで起動する()
        {
            // メインスレッドで await 継続を同期待ちするとデッドロックするため、全体をスレッドプールで実行する
            // Blocking the main thread on await continuations deadlocks; run the whole scenario on the thread pool
            Task.Run(RunScenarioAsync).GetAwaiter().GetResult();

            #region Internal

            static async Task RunScenarioAsync()
            {
                // ベースポートを占有（他Editor占有時も成立）
                // Occupy the base port with a dummy listener (test also holds if another Editor owns it)
                var basePortWasFree = TryListen(WebUiPortConfig.KestrelBasePort, out var blocker);

                var kestrel = new KestrelServer();
                // Assert 失敗時もポートを解放し、後続テストや Editor 起動に競合を持ち込まない
                // Release the ports even when an Assert throws, so later tests and Editor startups do not collide
                try
                {
                    await kestrel.StartAsync(new WebSocketHub());

                    Assert.AreNotEqual(WebUiPortConfig.KestrelBasePort, kestrel.ActualPort);
                    Assert.That(kestrel.ActualPort, Is.GreaterThan(WebUiPortConfig.KestrelBasePort));
                    Assert.That(kestrel.ActualPort, Is.LessThan(WebUiPortConfig.KestrelBasePort + WebUiPortConfig.PortSearchRange));
                }
                finally
                {
                    await kestrel.StopAsync();
                    if (basePortWasFree) blocker.Stop();
                }
            }

            static bool TryListen(int port, out TcpListener listener)
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                // ポートが既に他プロセスに占有されている場合は SocketException になる。OS ネットワーク境界の隔離
                // An already-occupied port throws SocketException; isolating the OS network boundary
                try
                {
                    listener.Start();
                    return true;
                }
                catch (SocketException)
                {
                    listener = null;
                    return false;
                }
            }

            #endregion
        }
    }
}

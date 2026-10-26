using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec.Listen
{
    // 受付が想定外に終わったら、接続情報を有効な入口として残さないことを固定する
    // Pin that an unexpected accept-loop exit never leaves the access details as a live entry
    public sealed class RemoteExecListenerFaultTest
    {
        private RemoteExecTestFiles _files;
        private bool _wasEnabled;
        private string AccessPath => Path.Combine(RemoteExecAccessFile.DirectoryPath, RemoteExecAccessFile.FileName);

        [SetUp]
        public void SetUp()
        {
            _wasEnabled = RemoteExecLaunchOption.IsEnabled;
            _files = new RemoteExecTestFiles();
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
        }

        [TearDown]
        public void TearDown()
        {
            RemoteExecActivation.Deactivate();
            RemoteExecLaunchOption.ResolveFromCommandLine(_wasEnabled ? new[] { "--remoteExec" } : Array.Empty<string>());
            _files.Restore();
        }

        [Test]
        public async Task 受付が想定外に終了したら接続情報とトークンを撤去する()
        {
            RemoteExecActivation.ActivateIfRequested();
            var port = JObject.Parse(File.ReadAllText(AccessPath)).Value<int>("port");
            Assert.IsNotNull(RemoteExecAccessFile.Token);

            // Stop()を経ずに待受のソケットだけを閉じ、受付の故障を作る
            // Close only the listener socket without Stop() to cause an accept fault
            LogAssert.Expect(LogType.Error, new Regex("待受が要求を受け取れなくなりました"));
            ReadListener().Close();

            // 撤去はメインスレッドへ合流してから行うので、反映を待つ
            // Withdrawal happens after joining the main thread, so wait for it to land
            for (var waited = 0; waited < 5000 && RemoteExecAccessFile.Token != null; waited += 50) await Task.Delay(50);

            Assert.IsNull(RemoteExecAccessFile.Token, "受付故障後もトークンが公開されています");
            Assert.IsFalse(File.Exists(AccessPath), "受付故障後も access.json が残っています");
            Assert.IsNull(ReadListener(), "受付故障後も故障した待受が現在の世代として残っています");
            Assert.IsFalse(await CanConnectAsync(port), $"故障した待受のポート {port} が開いたままです");
        }

        private static HttpListener ReadListener()
        {
            return (HttpListener)typeof(RemoteExecHttpListener).GetField("_listener", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        private static async Task<bool> CanConnectAsync(int port)
        {
            using var client = new TcpClient();
            // ローカルOSソケット境界。接続拒否は「閉じている」として呼び出し側のassertへ渡す
            // Local OS socket boundary; a refused connection is reported to the caller's assertion as closed
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port);
                return true;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused)
            {
                return false;
            }
        }
    }
}

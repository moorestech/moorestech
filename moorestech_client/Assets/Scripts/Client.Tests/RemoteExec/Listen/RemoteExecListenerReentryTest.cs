using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
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
    // 停止中の取消コールバックからの再有効化を拒否し、閉じられない待受を残さないことを固定する
    // Pin that reactivation from a mid-stop cancellation callback is refused and leaves no unclosable listener
    public sealed class RemoteExecListenerReentryTest
    {
        private static readonly Regex ReentryRefusedLog = new("待受の停止中に再び開く要求が来たため拒否しました");
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
        public async Task 再有効化の取消中に再入した有効化は拒否され撤去後に待受が残らない()
        {
            RemoteExecActivation.ActivateIfRequested();
            var firstPort = ReadAccessPort();
            var listenerAfterInnerActivate = new HttpListener[1];
            var reentered = false;

            // 取消コールバックから同じ有効化を呼び、内側で待受が作られないことを記録する
            // Call the same activation from the cancellation callback and record that no inner listener is created
            using var registration = ReadLifetime().Token.Register(() =>
            {
                RemoteExecActivation.ActivateIfRequested();
                listenerAfterInnerActivate[0] = ReadListener();
                reentered = true;
            });
            LogAssert.Expect(LogType.Error, ReentryRefusedLog);
            RemoteExecActivation.ActivateIfRequested();
            var outerPort = ReadAccessPort();

            Assert.IsTrue(reentered, "取消コールバックへ到達しませんでした");
            Assert.IsNull(listenerAfterInnerActivate[0], "停止中の再入で待受が作られました");

            // 撤去後は旧世代・新世代どちらのポートも閉じている
            // After withdrawal neither the old nor the new generation's port accepts connections
            RemoteExecActivation.Deactivate();
            Assert.IsFalse(await CanConnectAsync(firstPort), $"旧世代のポート {firstPort} が開いたままです");
            Assert.IsFalse(await CanConnectAsync(outerPort), $"新世代のポート {outerPort} が開いたままです");
        }

        [Test]
        public void 撤去の取消中に再入した有効化は拒否され接続情報も待受も残らない()
        {
            RemoteExecActivation.ActivateIfRequested();
            var reentered = false;

            // 撤去中の取消コールバックから有効化を呼ぶ
            // Call activation from the cancellation callback during withdrawal
            using var registration = ReadLifetime().Token.Register(() =>
            {
                RemoteExecActivation.ActivateIfRequested();
                reentered = true;
            });
            LogAssert.Expect(LogType.Error, ReentryRefusedLog);
            RemoteExecActivation.Deactivate();

            Assert.IsTrue(reentered, "取消コールバックへ到達しませんでした");
            Assert.IsNull(ReadListener(), "撤去後に再入した世代の待受が残っています");
            Assert.IsNull(RemoteExecAccessFile.Token, "撤去後にトークンが残っています");
            Assert.IsFalse(File.Exists(AccessPath), "撤去後に access.json が残っています");
        }

        private int ReadAccessPort()
        {
            return JObject.Parse(File.ReadAllText(AccessPath)).Value<int>("port");
        }

        private static HttpListener ReadListener()
        {
            return (HttpListener)typeof(RemoteExecHttpListener).GetField("_listener", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        private static CancellationTokenSource ReadLifetime()
        {
            return (CancellationTokenSource)typeof(RemoteExecHttpListener).GetField("_lifetime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
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

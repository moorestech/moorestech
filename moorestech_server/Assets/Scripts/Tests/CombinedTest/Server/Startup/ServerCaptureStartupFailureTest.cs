using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Context;
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server
{
    public class ServerCaptureStartupFailureTest
    {
        private string _root;
        private string _snapshots;
        private Socket _reserved;
        private ServerInstanceManager _manager;

        [SetUp]
        public void SetUp()
        {
            _root = null;
            _snapshots = null;
            _reserved = null;
            _manager = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_snapshots != null) Chmod(_snapshots, "755");
            _reserved?.Dispose();
            _manager?.Dispose();
            if (_manager != null && ServerContext.IsInitialized) ServerContext.GetService<WorldSnapshotRing>().Stop();
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
            if (_root != null) Directory.Delete(_root, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 削除拒否なら待受とスレッドと自動保存を開始しない(bool reservePort)
        {
            if (Environment.UserName == "root" || Application.platform == RuntimePlatform.WindowsEditor)
                Assert.Ignore("Unix非rootの削除権限拒否を使うテスト");
            _root = Path.Combine(Path.GetTempPath(), $"capture-start-failure-{Guid.NewGuid():N}");
            var world = WorldDataDirectory.FromWorldRoot(_root);
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(world, TestModDirectory.ForUnitTestModDirectory, "template", 0));
            _snapshots = world.SnapshotDirectory;
            Directory.CreateDirectory(_snapshots);
            File.WriteAllText(Path.Combine(_snapshots, WorldDataDirectory.SnapshotOwnerFileName), "old-owner");
            Chmod(_snapshots, "555");

            // 旧コードの資源漏れを起こさず、削除検証とbindの順序を測る
            // Reserve the port to detect ordering without leaking resources under the old implementation
            _reserved = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _reserved.Bind(new IPEndPoint(IPAddress.Any, 0));
            _reserved.Listen(1);
            var port = ((IPEndPoint)_reserved.LocalEndPoint).Port;
            if (!reservePort)
            {
                _reserved.Dispose();
                _reserved = null;
            }
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());
            _manager = new ServerInstanceManager(new[]
            {
                "--worldDirectory", _root, "--mapMode", "template", "--port", port.ToString(),
                "--autoSave", "true", "--serverDataDirectory", TestModDirectory.ForUnitTestModDirectory,
            });

            LogAssert.Expect(LogType.Error, new Regex("前セッションの常時記録の削除が権限で拒否"));
            Assert.Throws<UnauthorizedAccessException>(() => _manager.Start());
            Assert.AreEqual(0, _manager.BoundPort);
            foreach (var field in new[] { "_listener", "_connectionUpdateThread", "_gameUpdateThread", "_cancellationTokenSource" })
                Assert.IsNull(typeof(ServerInstanceManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager));
            Assert.IsFalse(ServerContext.GetService<WorldSnapshotRing>().IsActive);

            _reserved?.Dispose();
            _reserved = null;
            using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Assert.DoesNotThrow(() => probe.Bind(new IPEndPoint(IPAddress.Any, port)));
        }

        [Test]
        public void 記録開始後のbind失敗でもManagerが記録を破棄できる()
        {
            _root = Path.Combine(Path.GetTempPath(), $"capture-bind-failure-{Guid.NewGuid():N}");
            _reserved = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _reserved.Bind(new IPEndPoint(IPAddress.Any, 0));
            _reserved.Listen(1);
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());
            _manager = new ServerInstanceManager(new[]
            {
                "--worldDirectory", _root, "--mapMode", "template", "--port", ((IPEndPoint)_reserved.LocalEndPoint).Port.ToString(),
                "--autoSave", "true", "--serverDataDirectory", TestModDirectory.ForUnitTestModDirectory,
            });
            Assert.Throws<SocketException>(() => _manager.Start());
            Assert.IsTrue(ServerContext.GetService<WorldSnapshotRing>().IsActive);
            _manager.Dispose();
            Assert.IsFalse(ServerContext.GetService<WorldSnapshotRing>().IsActive);
            foreach (var path in Directory.GetFiles(WorldDataDirectory.FromWorldRoot(_root).SnapshotDirectory, "packets_*.bin"))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Assert.IsNotNull(stream);
            }
        }

        private static void Chmod(string path, string mode)
        {
            using var process = Process.Start(new ProcessStartInfo("chmod", $"{mode} \"{path}\"") { UseShellExecute = false });
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode);
        }
    }
}

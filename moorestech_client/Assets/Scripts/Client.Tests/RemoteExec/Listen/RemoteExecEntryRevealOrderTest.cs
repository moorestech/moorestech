using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Loading;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.RemoteExec.Listen
{
    // 入口（access.json とトークン）は Harmony を読み終えてから外へ見せることを固定する
    // Pin that the entry (access.json and the token) becomes visible only after Harmony has been loaded
    public sealed class RemoteExecEntryRevealOrderTest
    {
        private static readonly FieldInfo HarmonyField = typeof(RemoteExecHarmonyLoader).GetField("_harmony", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly List<string> _logsWithEntryBeforeHarmony = new();
        private RemoteExecTestFiles _files;
        private bool _wasEnabled;
        private object _previousHarmony;
        private string AccessPath => Path.Combine(RemoteExecAccessFile.DirectoryPath, RemoteExecAccessFile.FileName);

        [SetUp]
        public void SetUp()
        {
            _wasEnabled = RemoteExecLaunchOption.IsEnabled;
            _previousHarmony = HarmonyField.GetValue(null);
            _files = new RemoteExecTestFiles();
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
            RemoteExecActivation.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= RecordEntryVisibleBeforeHarmony;
            RemoteExecActivation.Deactivate();
            HarmonyField.SetValue(null, _previousHarmony);
            RemoteExecLaunchOption.ResolveFromCommandLine(_wasEnabled ? new[] { "--remoteExec" } : Array.Empty<string>());
            _files.Restore();
        }

        [Test]
        public void 入口はHarmonyを読み終えてから見せる()
        {
            // Editor が読み込み済みの Harmony を外し、Load が走るまで未読み込みに見せる
            // Detach the Harmony the Editor already loaded so it reads as unloaded until Load runs
            Assume.That(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == RemoteExecHarmonyLoader.HarmonyAssemblyName), "Editor に 0Harmony が読み込まれていない");
            HarmonyField.SetValue(null, null);
            Application.logMessageReceived += RecordEntryVisibleBeforeHarmony;

            RemoteExecActivation.ActivateIfRequested();

            Assert.IsNotNull(RemoteExecAccessFile.Token, "入口が開いていない");
            Assert.IsTrue(File.Exists(AccessPath), "access.json が公開されていない");
            CollectionAssert.IsEmpty(_logsWithEntryBeforeHarmony, "Harmony を読み終える前に入口が外から見えていた");
        }

        // ログの出る各時点で、入口が見えているのに Harmony が未読み込みなら、そのログを記録する
        // At every log point, record the log when the entry is visible while Harmony is still unloaded
        private void RecordEntryVisibleBeforeHarmony(string condition, string stackTrace, LogType type)
        {
            var entryVisible = RemoteExecAccessFile.Token != null || File.Exists(AccessPath);
            if (entryVisible && HarmonyField.GetValue(null) == null) _logsWithEntryBeforeHarmony.Add(condition);
        }
    }
}

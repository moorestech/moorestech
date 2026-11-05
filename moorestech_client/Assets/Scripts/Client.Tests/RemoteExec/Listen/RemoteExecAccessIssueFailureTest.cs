using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using Client.RemoteExec.Loading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec.Listen
{
    // access.json を書けない起動では、開いた待受を閉じて入口を残さないことを固定する
    // Pin that a boot unable to write access.json closes the opened listener and leaves no entry
    public sealed class RemoteExecAccessIssueFailureTest
    {
        private static readonly FieldInfo HarmonyField = typeof(RemoteExecHarmonyLoader).GetField("_harmony", BindingFlags.Static | BindingFlags.NonPublic);
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
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= BlockAccessPathAfterStaging;
            RemoteExecActivation.Deactivate();
            HarmonyField.SetValue(null, _previousHarmony);
            if (Directory.Exists(AccessPath)) Directory.Delete(AccessPath);
            RemoteExecLaunchOption.ResolveFromCommandLine(_wasEnabled ? new[] { "--remoteExec" } : Array.Empty<string>());
            _files.Restore();
        }

        [Test]
        public void 接続情報を書けなければ待受を閉じる()
        {
            // access.json の位置にディレクトリを置いて書き込みを失敗させる。Harmony は外して読み込みの有無を見る
            // Put a directory where access.json goes so the write fails; detach Harmony to see whether it gets loaded
            Directory.CreateDirectory(AccessPath);
            HarmonyField.SetValue(null, null);

            LogAssert.Expect(LogType.Error, new Regex("access.json を書けませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("入口を閉じました"));
            RemoteExecActivation.ActivateIfRequested();

            Assert.IsNull(ReadListener(), "接続情報を書けないのに待受が開いたまま残っています");
            Assert.IsNull(RemoteExecAccessFile.Token);
            Assert.IsNull(HarmonyField.GetValue(null), "接続情報を書けないのに Harmony を読み込みました");
        }

        [Test]
        public void 公開できなければ待受を閉じ公開待ちの接続情報も残さない()
        {
            // 公開待ちを書いた直後に正式な場所をディレクトリで塞ぎ、Harmony を読んだ後の公開を失敗させる
            // Block the official place with a directory right after staging so publishing after Harmony fails
            Application.logMessageReceived += BlockAccessPathAfterStaging;

            LogAssert.Expect(LogType.Error, new Regex("access.json を公開できませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("入口を閉じました"));
            RemoteExecActivation.ActivateIfRequested();

            Assert.IsTrue(Directory.Exists(AccessPath), "公開の前に正式な場所を塞げていない");
            Assert.IsNull(ReadListener(), "公開できないのに待受が開いたまま残っています");
            Assert.IsNull(RemoteExecAccessFile.Token);
            CollectionAssert.IsEmpty(Directory.GetFiles(RemoteExecAccessFile.DirectoryPath, "access-staged-*"), "公開待ちの接続情報が残っています");
        }

        private void BlockAccessPathAfterStaging(string condition, string stackTrace, LogType type)
        {
            if (condition.Contains("公開待ちに書きました")) Directory.CreateDirectory(AccessPath);
        }

        private static HttpListener ReadListener()
        {
            return (HttpListener)typeof(RemoteExecHttpListener).GetField("_listener", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }
    }
}

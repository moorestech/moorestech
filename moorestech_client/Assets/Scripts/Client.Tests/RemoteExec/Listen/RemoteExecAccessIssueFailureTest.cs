using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec.Listen
{
    // access.json を書けない起動では、開いた待受を閉じて入口を残さないことを固定する
    // Pin that a boot unable to write access.json closes the opened listener and leaves no entry
    public sealed class RemoteExecAccessIssueFailureTest
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
            if (Directory.Exists(AccessPath)) Directory.Delete(AccessPath);
            RemoteExecLaunchOption.ResolveFromCommandLine(_wasEnabled ? new[] { "--remoteExec" } : Array.Empty<string>());
            _files.Restore();
        }

        [Test]
        public void 接続情報を書けなければ待受を閉じる()
        {
            // access.json の位置にディレクトリを置いて書き込みを失敗させる
            // Put a directory where access.json goes so the write fails
            Directory.CreateDirectory(AccessPath);

            LogAssert.Expect(LogType.Error, new Regex("access.json を書けませんでした"));
            LogAssert.Expect(LogType.Error, new Regex("入口を閉じました"));
            RemoteExecActivation.ActivateIfRequested();

            Assert.IsNull(ReadListener(), "接続情報を書けないのに待受が開いたまま残っています");
            Assert.IsNull(RemoteExecAccessFile.Token);
        }

        private static HttpListener ReadListener()
        {
            return (HttpListener)typeof(RemoteExecHttpListener).GetField("_listener", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }
    }
}

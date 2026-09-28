using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecLedgerTest
    {
        private string _directory;
        private RemoteExecLedgerWriter _writer;

        [SetUp]
        public void SetUp()
        {
            // 実ユーザーのpid台帳と分離し、ディレクトリが無い状態から試す
            // Isolate the real user's pid ledger and start with no directory
            _directory = Path.Combine(Path.GetTempPath(), "RemoteExecLedgerTest-" + Guid.NewGuid().ToString("N"));
            _writer = new RemoteExecLedgerWriter(_directory, Process.GetCurrentProcess().Id, "session_123");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            if (File.Exists(_directory)) File.Delete(_directory);
        }

        [Test]
        public void 結果前に停止しても開始行に送信コードが残る()
        {
            Assert.IsFalse(Directory.Exists(_directory));
            var sequence = _writer.AppendStart(RemoteExecTarget.Client, "return 1;", out var written);
            Assert.IsTrue(written);
            var entries = Array.ConvertAll(File.ReadAllLines(_writer.FilePath), JObject.Parse);
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("start", entries[0].Value<string>("event"));
            Assert.AreEqual(sequence, entries[0].Value<long>("sequence"));
            Assert.AreEqual("client", entries[0].Value<string>("target"));
            Assert.AreEqual("return 1;", entries[0].Value<string>("code"));
        }

        [Test]
        public void 台帳名はPIDとセッション名を含む()
        {
            Assert.AreEqual("ledger-42-session_123.jsonl", RemoteExecLedgerWriter.FileNameFor(42, "session_123"));
            Assert.IsTrue(RemoteExecLedger.IsLedgerFileName(Path.GetFileName(_writer.FilePath)));
        }

        [Test]
        public void 結果行は成功種別を記録する()
        {
            var sequence = _writer.AppendStart(RemoteExecTarget.Client, "return 1;", out _);
            Assert.IsTrue(_writer.AppendResult(sequence, RemoteExecOutcome.Succeeded));
            var entries = Array.ConvertAll(File.ReadAllLines(_writer.FilePath), JObject.Parse);
            Assert.AreEqual("Succeeded", entries[1].Value<string>("outcome"));
            Assert.AreEqual(sequence, entries[1].Value<long>("sequence"));
        }

        [Test]
        public void 台帳を書けなければ失敗を呼び出し元へ返す()
        {
            File.WriteAllText(_directory, "blocking file");
            LogAssert.Expect(LogType.Error, new Regex("台帳に書けませんでした"));
            _writer.AppendStart(RemoteExecTarget.Client, "return 1;", out var written);
            Assert.IsFalse(written);
            LogAssert.Expect(LogType.Error, new Regex("台帳に書けませんでした"));
            Assert.IsFalse(_writer.AppendResult(1, RemoteExecOutcome.ServerUnavailable));
        }
    }
}

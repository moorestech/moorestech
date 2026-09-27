using System;
using System.Diagnostics;
using System.IO;
using Client.RemoteExec.Access;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

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
            _writer = new RemoteExecLedgerWriter(_directory, Process.GetCurrentProcess().Id);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void 結果前に停止しても開始行に送信コードが残る()
        {
            Assert.IsFalse(Directory.Exists(_directory));
            var sequence = _writer.AppendStart("client", "return 1;");
            var entries = Array.ConvertAll(File.ReadAllLines(_writer.FilePath), JObject.Parse);
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("start", entries[0].Value<string>("event"));
            Assert.AreEqual(sequence, entries[0].Value<long>("sequence"));
            Assert.AreEqual("client", entries[0].Value<string>("target"));
            Assert.AreEqual("return 1;", entries[0].Value<string>("code"));
        }
    }
}

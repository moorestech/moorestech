using System;
using System.Reflection;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.RemoteExec;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    // ヘッダへ遠隔実行の印を立てる配線そのものを固定する。値の往復テストだけでは、配線が消えても記録は成立してしまう
    // Pins the wiring that stamps remote execution onto the header; a value round-trip alone still passes once the wiring is gone
    public class ProgressRecorderRemoteExecHeaderTest
    {
        [Test]
        public void 進行記録のヘッダは起動オプションの遠隔実行フラグを読む()
        {
            var createHeader = FindCreateHeader();
            var isEnabled = typeof(RemoteExecLaunchOption).GetProperty(nameof(RemoteExecLaunchOption.IsEnabled)).GetGetMethod();

            Assert.That(createHeader, Is.Not.Null, "ProgressRecorderのヘッダ組み立てが見つからない（改名したらこのテストも追随させる）");
            Assert.That(MethodCallInspector.ContainsCall(createHeader, isEnabled), Is.True,
                "ヘッダが遠隔実行の起動オプションを読まなくなった（遠隔実行ありの記録が集計と自動修正へ混ざる）");
        }

        // ローカル関数はコンパイラが生成した名前になる。名前の形に依存せず本体を探す
        // A local function gets a compiler-generated name, so the body is found without relying on its shape
        private static MethodInfo FindCreateHeader()
        {
            foreach (var method in typeof(ProgressRecorder).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (method.Name.IndexOf("CreateHeader", StringComparison.Ordinal) < 0) continue;
                if (method.ReturnType == typeof(ProgressRecordHeader)) return method;
            }
            return null;
        }
    }
}

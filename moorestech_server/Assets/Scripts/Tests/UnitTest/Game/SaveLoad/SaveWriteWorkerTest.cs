using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Paths;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Writer;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveWriteWorkerTest
    {
        [Test]
        public void 捕捉していない例外が出ても書き出しスレッドは死なず次の書き出しを続ける()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-writer-{Guid.NewGuid():N}.json");
            var (worker, data) = CreateWorkerAndCapture(savePath);

            // 想定外の例外も無音で縮退させずエラーログを出す契約なので、その1件を想定として宣言する
            // An unexpected exception must log instead of degrading silently, so declare that one error as expected
            LogAssert.Expect(LogType.Error, new Regex("^セーブの書き出しが想定外の例外で失敗しました"));

            // 空の書き出し先はIOでも権限でもない例外になり、個別catchのどちらにも該当しない
            // An empty destination raises neither an IO nor an access exception, so no specific catch handles it
            worker.Enqueue(new SaveWriteJob(1, SaveWriteKind.PlayerSave, data, string.Empty, false));
            worker.WaitForIdle();

            Assert.IsTrue(worker.TryDequeueCompletion(SaveWriteKind.PlayerSave, out var failed), "完了通知が積まれていない");
            Assert.IsFalse(failed.Success);
            Assert.IsFalse(worker.HasInFlight, "在庫が戻っていない");

            // スレッドが生き残っていることを、後続ジョブが実際に書けることで観測する
            // Observe that the thread survived by checking a later job actually writes
            worker.Enqueue(new SaveWriteJob(2, SaveWriteKind.PlayerSave, data, savePath, false));
            worker.WaitForIdle();

            Assert.IsTrue(worker.TryDequeueCompletion(SaveWriteKind.PlayerSave, out var succeeded), "後続の完了通知が積まれていない");
            Assert.IsTrue(succeeded.Success, "後続の書き出しが失敗している");
            Assert.IsTrue(File.Exists(savePath));
            File.Delete(savePath);
        }

        private static (SaveWriteWorker worker, WorldSaveAllInfoV1 data) CreateWorkerAndCapture(string savePath)
        {
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            return (provider.GetRequiredService<SaveWriteWorker>(), provider.GetRequiredService<AssembleSaveJsonText>().Capture());
        }
    }
}

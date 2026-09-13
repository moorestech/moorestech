using Game.Paths;
using System;
using System.IO;
using Game.SaveLoad;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using System.Text.RegularExpressions;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class WorldSaveCoordinatorTest
    {
        [Test]
        public void 複数の保存要求を一回の保存へまとめる()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-coordinator-{Guid.NewGuid():N}.json");
            var coordinator = CreateCoordinator(savePath);

            coordinator.RequestSave();
            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(File.Exists(savePath));
            Assert.IsFalse(coordinator.HasPendingSave);

            // 消化済み要求で再保存されないことをファイルが再生成されないことで観測する
            // Verify consumed requests trigger no re-save by checking the file is not recreated
            File.Delete(savePath);
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void 書き出しに失敗した要求は次回に再実行する()
        {
            // 保存先ディレクトリの位置にファイルを置き、ディレクトリ作成を失敗させる
            // Put a file where the save directory should be so directory creation fails
            var saveDirectory = Path.Combine(Path.GetTempPath(), $"moorestech-coordinator-{Guid.NewGuid():N}");
            File.WriteAllText(saveDirectory, "blocker");
            var savePath = Path.Combine(saveDirectory, "save.json");
            var coordinator = CreateCoordinator(savePath);

            // 書き出し失敗は無音で縮退させずエラーログを出す契約なので、その1件を想定として宣言する
            // A failed write must log instead of degrading silently, so declare that one error as expected
            LogAssert.Expect(LogType.Error, new Regex("^セーブの書き出しに失敗しました"));

            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(coordinator.HasPendingSave, "失敗した書き出しが完了扱いになっている");

            File.Delete(saveDirectory);
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(File.Exists(savePath));
            Assert.IsFalse(coordinator.HasPendingSave);
            Directory.Delete(saveDirectory, true);
        }

        // 恒久的な失敗を毎tick再取り込みすると、tickスレッドが全世界Captureを毎tick負って実質フリーズする
        // Recapturing a permanent failure every tick loads the tick thread with a full-world capture per tick and effectively freezes it
        [Test]
        public void 恒久的に失敗する書き出しは有限回で諦める()
        {
            var saveDirectory = Path.Combine(Path.GetTempPath(), $"moorestech-coordinator-{Guid.NewGuid():N}");
            File.WriteAllText(saveDirectory, "blocker");
            var savePath = Path.Combine(saveDirectory, "save.json");
            var coordinator = CreateCoordinator(savePath);

            // 失敗3回ぶんの書き出しエラーと、諦めた理由のエラーが出る契約
            // Three write failures plus one give-up reason are the contract
            for (var i = 0; i < 3; i++) LogAssert.Expect(LogType.Error, new Regex("^セーブの書き出しに失敗しました"));
            LogAssert.Expect(LogType.Error, new Regex("^セーブの書き出しに3回失敗したため要求1を諦めます"));

            coordinator.RequestSave();
            for (var attempt = 0; attempt < 10; attempt++)
            {
                coordinator.SaveIfRequested();
                coordinator.WaitForPendingWrites();
                if (!coordinator.HasPendingSave) break;
            }

            Assert.IsFalse(coordinator.HasPendingSave, "恒久的な失敗を無限に再試行し続けている");

            // 諦めは待ちを明けるだけで世界は保存されていない。終了経路が成功と区別できる状態として残す
            // Giving up merely clears the wait while the world stays unsaved, so the shutdown path needs a state that tells it from success
            Assert.IsTrue(coordinator.HasAbandonedSave, "諦めた保存が成功と同じ状態へ畳まれている");
            Assert.AreEqual(1L, coordinator.AbandonedGeneration, "諦めた要求番号が残っていない");

            // 諦めた後は再取り込みしない。ここで書き出しが起きるなら毎tick全世界Captureが続いている
            // No recapture after giving up; another write here would mean the per-tick full-world capture continues
            File.Delete(saveDirectory);
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsFalse(File.Exists(savePath), "諦めた要求が再取り込みされている");

            // 書き出せたら諦めは解ける。残したままだと以後の正常な終了まで保存失敗を名乗り続ける
            // A successful write clears the give-up; keeping it would make every later healthy shutdown claim a failed save
            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            coordinator.WaitForPendingWrites();
            Assert.IsTrue(File.Exists(savePath), "諦めた後の新しい要求が書き出されていない");
            Assert.IsFalse(coordinator.HasAbandonedSave, "書き出せたのに諦めた状態が残っている");
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        private static WorldSaveCoordinator CreateCoordinator(string savePath)
        {
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            return provider.GetRequiredService<WorldSaveCoordinator>();
        }
    }
}

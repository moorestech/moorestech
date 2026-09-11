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

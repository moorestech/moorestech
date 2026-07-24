using Game.Paths;
using System;
using System.IO;
using Game.SaveLoad;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

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
            Assert.IsTrue(File.Exists(savePath));

            // 消化済み要求で再保存されないことをファイルが再生成されないことで観測する
            // Verify consumed requests trigger no re-save by checking the file is not recreated
            File.Delete(savePath);
            coordinator.SaveIfRequested();
            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void 保存自体が完了しなかった要求は次回に再実行する()
        {
            var saveDirectory = Path.Combine(Path.GetTempPath(), $"moorestech-coordinator-{Guid.NewGuid():N}");
            var savePath = Path.Combine(saveDirectory, "save.json");
            var coordinator = CreateCoordinator(savePath);
            coordinator.RequestSave();

            // 保存先ディレクトリが無い間は保存が失敗し、要求は未消化のまま残る
            // While the target directory is missing the save fails and the request stays pending
            Assert.Catch<IOException>(coordinator.SaveIfRequested);
            Directory.CreateDirectory(saveDirectory);
            coordinator.SaveIfRequested();

            Assert.IsTrue(File.Exists(savePath));
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

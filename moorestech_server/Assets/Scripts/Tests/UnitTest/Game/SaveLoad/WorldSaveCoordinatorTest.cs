using System;
using Game.SaveLoad;
using Game.SaveLoad.Interface;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class WorldSaveCoordinatorTest
    {
        [Test]
        public void 複数の保存要求を一回の保存へまとめる()
        {
            var saver = new FakeWorldSaveDataSaver();
            var coordinator = new WorldSaveCoordinator(saver);

            coordinator.RequestSave();
            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            coordinator.SaveIfRequested();

            Assert.AreEqual(1, saver.SaveCount);
        }

        [Test]
        public void 保存中に届いた要求を次回の保存へ残す()
        {
            var saver = new FakeWorldSaveDataSaver();
            var coordinator = new WorldSaveCoordinator(saver);
            saver.OnSave = coordinator.RequestSave;

            coordinator.RequestSave();
            coordinator.SaveIfRequested();
            saver.OnSave = null;
            coordinator.SaveIfRequested();

            Assert.AreEqual(2, saver.SaveCount);
        }

        [Test]
        public void 保存自体が完了しなかった要求は次回に再実行する()
        {
            var saver = new FakeWorldSaveDataSaver { ThrowOnNextSave = true };
            var coordinator = new WorldSaveCoordinator(saver);
            coordinator.RequestSave();

            Assert.Throws<InvalidOperationException>(coordinator.SaveIfRequested);
            coordinator.SaveIfRequested();

            Assert.AreEqual(2, saver.SaveCount);
        }

        private sealed class FakeWorldSaveDataSaver : IWorldSaveDataSaver
        {
            public int SaveCount;
            public bool ThrowOnNextSave;
            public Action OnSave;

            public void Save()
            {
                SaveCount++;
                OnSave?.Invoke();
                if (!ThrowOnNextSave) return;
                ThrowOnNextSave = false;
                throw new InvalidOperationException("test save failure");
            }
        }
    }
}

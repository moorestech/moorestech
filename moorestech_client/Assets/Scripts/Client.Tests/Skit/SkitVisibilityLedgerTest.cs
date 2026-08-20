using System;
using System.Collections.Generic;
using Client.Game.Skit;
using CommandForgeGenerator.Command;
using NUnit.Framework;

namespace Client.Tests.Skit
{
    public class SkitVisibilityLedgerTest
    {
        [Test]
        public void RestoreTouchesOnlyTheWindowsTheSkitSwitchedOff()
        {
            var background = new RecordingWindow();
            var block = new RecordingWindow();
            var worldObject = new RecordingWindow();
            var entity = new RecordingWindow();
            var ledger = new SkitVisibilityLedger(background, block, worldObject, entity);

            // 背景と世界オブジェクトだけを消したスキットが中断しても、その2窓口だけが戻る
            // A skit that hid only the background and world objects restores exactly those two on abort
            ((ISkitEnvironmentRoot)ledger).SetActive(false);
            ((ISkitWorldObjectControl)ledger).SetActive(false);
            ledger.RestoreHiddenWindows();

            CollectionAssert.AreEqual(new[] { false, true }, background.ReceivedValues);
            CollectionAssert.AreEqual(new[] { false, true }, worldObject.ReceivedValues);
            CollectionAssert.IsEmpty(block.ReceivedValues);
            CollectionAssert.IsEmpty(entity.ReceivedValues);
        }

        [Test]
        public void RestoreSkipsWindowsAlreadyTurnedBackOnByTheSkit()
        {
            var entity = new RecordingWindow();
            var ledger = new SkitVisibilityLedger(new RecordingWindow(), new RecordingWindow(), new RecordingWindow(), entity);

            // スキット自身が戻した窓口は台帳から外れ、終了時に二度目のtrueが流れない
            // A window the skit turned back on leaves the ledger, so no second true reaches it at the end
            ((ISkitEntityObjectControl)ledger).SetActive(false);
            ((ISkitEntityObjectControl)ledger).SetActive(true);
            ledger.RestoreHiddenWindows();

            CollectionAssert.AreEqual(new[] { false, true }, entity.ReceivedValues);
        }

        [Test]
        public void RestoreStillReachesAWindowWhoseHideThrew()
        {
            var worldObject = new ThrowingOnHideWindow();
            var ledger = new SkitVisibilityLedger(new RecordingWindow(), new RecordingWindow(), worldObject, new RecordingWindow());

            // 非表示の途中で落ちた窓口も台帳に載っているため、隠れたまま取り残されない
            // A window that threw midway through hiding is still on the ledger, so it cannot stay hidden forever
            Assert.Throws<InvalidOperationException>(() => ((ISkitWorldObjectControl)ledger).SetActive(false));
            ledger.RestoreHiddenWindows();

            CollectionAssert.AreEqual(new[] { true }, worldObject.ReceivedValues);
        }

        private class RecordingWindow : ISkitEnvironmentRoot, ISkitBlockObjectControl, ISkitWorldObjectControl, ISkitEntityObjectControl
        {
            public readonly List<bool> ReceivedValues = new();

            public virtual void SetActive(bool enable)
            {
                ReceivedValues.Add(enable);
            }
        }

        private sealed class ThrowingOnHideWindow : RecordingWindow
        {
            public override void SetActive(bool enable)
            {
                if (!enable) throw new InvalidOperationException("hide failed");
                base.SetActive(enable);
            }
        }
    }
}

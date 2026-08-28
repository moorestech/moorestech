using System;
using Client.Starter.EventMode;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.EventMode
{
    public class EventModeStartGateTest
    {
        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE", null);
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE_EDITOR", null);
        }

        // 出展モードでない起動は1フレームも待たず、無操作監視も作らない
        // A non-event-mode boot waits zero frames and creates no idle watcher
        [Test]
        public void 出展モードでなければ即座に完了し監視も作らない()
        {
            var task = EventModeStartGate.WaitForLanguageSelectionAsync();

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            Assert.AreEqual(0, Object.FindObjectsByType<EventIdleQuitWatcher>(FindObjectsSortMode.None).Length);
        }

        // WebUiHost未起動のフォールバックはEditModeで検証できない（DontDestroyOnLoadがPlayMode専用APIのため）。
        // The no-WebUiHost fallback is not EditMode-testable because DontDestroyOnLoad is a PlayMode-only API.
        // 当該分岐はコードレビューとReleaseビルドの通し確認（ADR 0040「実機確認」）で担保する。
        // That branch is covered by code review and the Release build walkthrough recorded in ADR 0040.
    }
}

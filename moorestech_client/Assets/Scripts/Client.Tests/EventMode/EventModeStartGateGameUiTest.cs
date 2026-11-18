using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Client.Localization;
using Client.Starter.EventMode;
using Client.Ui.State.Commands;
using Client.Ui.State.EventMode;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EventMode
{
    /// <summary>
    /// UITK の言語ゲートの待ちを検証
    /// Pins the wait on the UITK language gate
    /// </summary>
    public class EventModeStartGateGameUiTest
    {
        private const string IdleTimeoutSeconds = "180";

        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void SaveLanguage()
        {
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
        }

        [TearDown]
        public void RestoreLanguage()
        {
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        // ゲートを開き、選択後に武装する
        // Opens the gate and arms after the choice
        [UnityTest]
        public IEnumerator GameUiの系ではゲートを開き選択の後に武装する()
        {
            var calls = new List<string>();
            var screen = new RecordingGateScreen(calls);

            var task = EventModeStartGate.WaitOnGameUiAsync(Settings("1"), screen, new RecordingArmer(calls), CancellationToken.None);

            // 選ぶまでは開いたまま武装しない
            // Until the choice it stays open and unarmed
            CollectionAssert.AreEqual(new[] { "open" }, calls);
            Assert.IsTrue(screen.OpenedGate.IsWaitingSelection);

            Assert.IsInstanceOf<UiCommandAccepted>(screen.OpenedGate.SelectLanguage("english"));
            yield return task.ToCoroutine();

            CollectionAssert.AreEqual(new[] { "open", "arm:180" }, calls);
        }

        [Test]
        public void 出展モードでなければ画面を開かず武装もしない()
        {
            var calls = new List<string>();

            var task = EventModeStartGate.WaitOnGameUiAsync(Settings(null), new RecordingGateScreen(calls), new RecordingArmer(calls), CancellationToken.None);

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            CollectionAssert.IsEmpty(calls);
        }

        // 選択の待ちは上限を持たないので、終了のキャンセルで画面を開いたまま武装せずに抜ける
        // The wait for the choice is unbounded, so an exit cancellation leaves with the screen opened and without arming
        [Test]
        public void 出展モードなら画面を開き終了のキャンセルで武装せずに抜ける()
        {
            var calls = new List<string>();
            using var exit = new CancellationTokenSource();

            var task = EventModeStartGate.WaitOnGameUiAsync(Settings("1"), new RecordingGateScreen(calls), new RecordingArmer(calls), exit.Token);

            CollectionAssert.AreEqual(new[] { "open" }, calls);
            exit.Cancel();
            Assert.IsTrue(task.Status.IsCanceled());
            CollectionAssert.AreEqual(new[] { "open" }, calls);
        }

        private static EventExhibitionSettings Settings(string enable)
        {
            return EventExhibitionSettings.Parse(new EventModeEnvironmentValues { Enable = enable, EditorOptIn = "1", IdleTimeoutSeconds = IdleTimeoutSeconds }, true);
        }

        // 開いたことと開いたゲートを記録する画面
        // Screen that records the open call and the opened gate
        public sealed class RecordingGateScreen : IEventLanguageGateScreen
        {
            private readonly List<string> _calls;

            public EventLanguageGate OpenedGate { get; private set; }

            public RecordingGateScreen(List<string> calls)
            {
                _calls = calls;
            }

            public void Open(EventLanguageGate gate)
            {
                _calls.Add("open");
                OpenedGate = gate;
            }
        }

        // 武装の呼ばれ方を同じ列へ記録する
        // Records arming calls into the same list
        public sealed class RecordingArmer : IEventIdleWatchArmer
        {
            private readonly List<string> _calls;

            public RecordingArmer(List<string> calls)
            {
                _calls = calls;
            }

            public void ArmIdleWatch(int idleTimeoutSeconds)
            {
                _calls.Add($"arm:{idleTimeoutSeconds}");
            }
        }
    }
}

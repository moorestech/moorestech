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
    /// GameUi の系の待ちと Web ホスト無しの縮退を、画面と武装の窓口の呼ばれ方で押さえる
    /// Pins the GameUi-system wait and the no-web-host fallback through how the screen and arming windows are called
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

        // GameUi の系は DI より前に UITK のゲートを開き、選ばれてから武装する
        // The GameUi system opens the UITK gate before DI and arms only after the choice
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

        // 武装の窓口を注入するので、Web の系の英語フォールバックも EditMode で確かめられる（DontDestroyOnLoad に触れない）
        // With the arming window injected, the Web system's English fallback is verifiable in EditMode too (no DontDestroyOnLoad)
        [Test]
        public void Webの系でWebホストが無ければ英語のまま武装だけする()
        {
            var calls = new List<string>();
            LogAssert.Expect(LogType.Error, "EventModeStartGate: WebUiHostが起動しておらず言語選択を出せないため英語のまま開始します");

            EventModeStartGate.StartInEnglishWithoutWebHost(Settings("1"), new RecordingArmer(calls));

            CollectionAssert.AreEqual(new[] { "arm:180" }, calls);
        }

        [Test]
        public void 出展モードでなければGameUiの系もWebホスト無しの縮退も画面を開かず武装もしない()
        {
            var calls = new List<string>();

            var task = EventModeStartGate.WaitOnGameUiAsync(Settings(null), new RecordingGateScreen(calls), new RecordingArmer(calls), CancellationToken.None);
            EventModeStartGate.StartInEnglishWithoutWebHost(Settings(null), new RecordingArmer(calls));

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            CollectionAssert.IsEmpty(calls);
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

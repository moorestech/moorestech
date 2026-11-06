using Client.Localization;
using Client.Ui.State.EventMode;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.EventMode;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EventMode
{
    /// <summary>
    /// 登録済みの言語選択アクションの入口
    /// Entry of the registered language selection action
    /// </summary>
    public class EventLanguageGateActionTest
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 言語の保存値を退避して辞書を読み込む
            // Stash the stored language and load the dictionary
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void 未知のlocaleは失敗し同じ言語の2回目は選択済みを返す()
        {
            var hub = new WebSocketHub();
            var gate = new EventLanguageGate(true);
            EventLanguageGateActions.Register(hub, gate);
            var action = hub.ResolveAction("event_mode.select_language");

            // payload → SelectLanguage → FromCommandResult の経路を通す
            // Go through payload, SelectLanguage and FromCommandResult
            var unknown = action.ExecuteAsync(JObject.Parse("{\"locale\":\"klingon\"}")).GetAwaiter().GetResult();
            Assert.IsFalse(unknown.Ok);
            Assert.AreEqual("unknown_locale", unknown.Error);
            Assert.IsTrue(gate.IsWaitingSelection);

            var first = action.ExecuteAsync(JObject.Parse("{\"locale\":\"english\"}")).GetAwaiter().GetResult();
            Assert.IsTrue(first.Ok);
            Assert.IsFalse(gate.IsWaitingSelection);
            Assert.AreEqual("english", Localize.GetCurrentLanguageCode());

            var second = action.ExecuteAsync(JObject.Parse("{\"locale\":\"english\"}")).GetAwaiter().GetResult();
            Assert.IsFalse(second.Ok);
            Assert.AreEqual("already_selected", second.Error);
        }
    }
}

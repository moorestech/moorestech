using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Client.Localization;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUi.Localization
{
    public class LocalizationLanguageSelectionContractTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 言語選択の静的状態と保存値を既知の英語へ揃える
            // Put the static locale state and persisted value into a known English state
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, Localize.DefaultLanguageCode);
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // テスト前の保存値を復元して他テストへの漏れを防ぐ
            // Restore the persisted value from before the test to prevent state leakage
            if (hadSavedLanguageCode)
                PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else
                PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);

            PlayerPrefs.Save();
            Localize.Initialize();
        }

        [Test]
        public void LanguagesRouteReturnsGeneratedCatalogAsCamelCaseJson()
        {
            // 実Kestrelルートを通してHTTP公開契約を検証する
            // Verify the public HTTP contract through the real Kestrel route
            Task.Run(RunScenarioAsync).GetAwaiter().GetResult();

            #region Internal

            static async Task RunScenarioAsync()
            {
                var kestrel = new KestrelServer();
                try
                {
                    await kestrel.StartAsync(new WebSocketHub());
                    using var client = new HttpClient();
                    var response = await client.GetAsync(
                        $"http://127.0.0.1:{kestrel.ActualPort}/api/i18n-languages");
                    var body = await response.Content.ReadAsStringAsync();

                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.AreEqual("application/json; charset=utf-8",
                        response.Content.Headers.ContentType?.ToString());
                    Assert.IsTrue(JToken.DeepEquals(
                        JArray.Parse(
                            "[{\"code\":\"english\",\"displayName\":\"English\"}," +
                            "{\"code\":\"japanese\",\"displayName\":\"日本語\"}," +
                            "{\"code\":\"german\",\"displayName\":\"Deutsch\"}]"),
                        JArray.Parse(body)));
                }
                finally
                {
                    await kestrel.StopAsync();
                }
            }

            #endregion
        }

        [Test]
        public async Task RegisteredSetLocaleActionPersistsAndPublishesOneChange()
        {
            var hub = new WebSocketHub();
            LocalizationActions.Register(hub);
            var handler = GetRegisteredSetLocaleHandler(hub);
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            // 登録済み実ハンドラから言語切替の全副作用を通す
            // Drive all locale-switch side effects through the registered real handler
            var result = await handler.ExecuteAsync(
                JObject.Parse("{\"locale\":\"japanese\"}"));

            Assert.IsTrue(result.Ok);
            Assert.IsNull(result.Error);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual("japanese",
                PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
            Assert.AreEqual(1, eventCount);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown")]
        public async Task SetLocaleActionRejectsInvalidLocaleWithoutSideEffects(
            string locale)
        {
            var handler = new SetLocaleActionHandler();
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);
            var payload = locale == null
                ? JObject.Parse("{\"locale\":null}")
                : new JObject { ["locale"] = locale };

            // 外部入力の不正値は一律の失敗契約へ正規化する
            // Normalize invalid external values to one failure contract
            var result = await handler.ExecuteAsync(payload);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_locale", result.Error);
            Assert.AreEqual(Localize.DefaultLanguageCode,
                Localize.GetCurrentLanguageCode());
            Assert.AreEqual(Localize.DefaultLanguageCode,
                PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public async Task SetLocaleActionRejectsNullPayloadWithoutSideEffects()
        {
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            var result = await new SetLocaleActionHandler().ExecuteAsync(null);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_locale", result.Error);
            Assert.AreEqual(Localize.DefaultLanguageCode,
                Localize.GetCurrentLanguageCode());
            Assert.AreEqual(0, eventCount);
        }

        private static IActionHandler GetRegisteredSetLocaleHandler(
            WebSocketHub hub)
        {
            // 公開動作を駆動する実レジストリから登録済みハンドラを得る
            // Read the registered handler from the real registry that drives public behavior
            var field = typeof(WebSocketHub).GetField(
                "_actionHandlers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var handlers =
                (ConcurrentDictionary<string, IActionHandler>)field.GetValue(hub);

            Assert.IsTrue(
                handlers.TryGetValue("localization.setLocale", out var handler));
            Assert.IsInstanceOf<SetLocaleActionHandler>(handler);
            return handler;
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using Client.Localization;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// 現在言語を snapshot と revision 付き event で配信する。
    /// Publishes the current locale as a snapshot and revisioned events.
    /// </summary>
    public class LocalizationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "localization.current";

        // hubは再生成され得るため、登録済みの印はhubごとに持つ。静的boolだと2回目のPlayModeで登録が飛ぶ
        // The hub can be recreated, so the "already registered" mark is per hub; a static bool would skip registration on a second Play Mode
        private static readonly ConditionalWeakTable<WebSocketHub, object> Registered = new();

        // 登録口。開始ゲートより前に要る一方ゲートを通らない接続経路でも要るので、冪等にして両方から呼ぶ
        // The registration port; the gates need it first but paths that skip them need it too, so it is idempotent and called from both
        // ゲートより後に登録していた頃は、Web側のI18nProviderが辞書を取れずゲート文言だけが常にfallback表示だった（ADR 0060 裁定10）
        // While it registered after the gates, the web I18nProvider had no dictionary and the gate texts alone always fell back (ADR 0060 adjudication 10)
        public static void EnsureRegistered(WebSocketHub hub)
        {
            if (Registered.TryGetValue(hub, out _)) return;
            Registered.Add(hub, new object());
            hub.RegisterTopic(TopicName, new LocalizationTopic(hub));
        }

        private readonly WebSocketHub _hub;
        private readonly IDisposable _languageSubscription;

        public LocalizationTopic(WebSocketHub hub)
        {
            _hub = hub;

            // uGUIの既存変更通知を購読し、辞書本体はHTTP経由の単一経路に保つ
            // Observe the existing uGUI notification and keep dictionary bodies on the HTTP path
            _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ =>
                _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _languageSubscription.Dispose();
        }

        private static string BuildJson()
        {
            return WebUiJson.Serialize(new LocalizationData
            {
                Locale = Localize.GetCurrentLanguageCode(),
                Revision = Localize.GetDictionaryRevision(),
            });
        }

        private class LocalizationData
        {
            public string Locale;
            public long Revision;
        }
    }
}

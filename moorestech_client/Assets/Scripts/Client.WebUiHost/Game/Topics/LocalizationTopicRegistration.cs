using System.Runtime.CompilerServices;
using Client.WebUiHost.Boot;

namespace Client.WebUiHost.Game.Topics
{
    // 現在言語トピックの登録口。開始ゲートより前に要る一方、ゲートを通らない接続経路でも要るので冪等にして両方から呼ぶ
    // The registration port for the current-locale topic; the gates need it first, but paths that skip the gates need it too, so it is idempotent and called from both
    // ゲートより後に登録していた頃は、Web側のI18nProviderが辞書を取れずゲート文言だけが常にfallback表示だった（ADR 0060 裁定10）
    // While it registered after the gates, the web I18nProvider had no dictionary and the gate texts alone always fell back (ADR 0060 adjudication 10)
    public static class LocalizationTopicRegistration
    {
        // hub は再生成され得るため、済み印は hub ごとに持つ。静的boolだと2回目のPlayModeで登録が飛ぶ
        // The hub can be recreated, so the "already done" mark is per hub; a static bool would skip the registration on a second Play Mode
        private static readonly ConditionalWeakTable<WebSocketHub, object> Registered = new();

        public static void EnsureRegistered(WebSocketHub hub)
        {
            if (Registered.TryGetValue(hub, out _)) return;
            Registered.Add(hub, new object());
            hub.RegisterTopic(LocalizationTopic.TopicName, new LocalizationTopic(hub));
        }
    }
}

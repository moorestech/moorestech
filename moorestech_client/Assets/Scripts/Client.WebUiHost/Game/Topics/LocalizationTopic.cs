using System;
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
                Locale = Localize.CurrentLanguageCode,
            });
        }

        private class LocalizationData
        {
            public string Locale;
        }
    }
}
